using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Comprobante.Core.Catalogos;
using Comprobante.Core.Xml;

namespace Comprobante.Core.Validacion.Reglas;

/// <summary>
/// Lo que las reglas necesitan para revisar un comprobante: el XML, los catálogos (los chicos completos y, de los grandes,
/// lo que el comprobante usa) y los datos que casi todas consultan (tipo, moneda, fecha). Aquí también se anotan los hallazgos.
/// </summary>
internal sealed class ContextoValidacion
{
    public const string RfcGenericoNacional = "XAXX010101000";
    public const string RfcGenericoExtranjero = "XEXX010101000";

    /// <summary>Un XML muy roto puede dar miles de hallazgos iguales: se guardan los primeros y se cuenta el resto.</summary>
    public const int MaximoDeHallazgos = 300;

    private const string FormatoDeFecha = "yyyy-MM-dd'T'HH:mm:ss";
    private const string EspacioComercioExterior = "http://www.sat.gob.mx/ComercioExterior";

    private readonly List<Hallazgo> _hallazgos = [];

    public ContextoValidacion(
        XDocument documento,
        CatalogosPequenos catalogos,
        IReadOnlyDictionary<string, ClaveProdServ> clavesProdServ,
        IReadOnlyDictionary<string, ClaveUnidad> clavesUnidad,
        IReadOnlyDictionary<string, CodigoPostal> codigosPostales,
        DateTimeOffset ahora)
    {
        Raiz = documento.Root!;
        Catalogos = catalogos;
        ClavesProdServ = clavesProdServ;
        ClavesUnidad = clavesUnidad;
        CodigosPostales = codigosPostales;
        Ahora = ahora;

        Tipo = Raiz.Valor("TipoDeComprobante");
        Moneda = Raiz.Valor("Moneda") is { } clave && catalogos.Monedas.TryGetValue(clave, out var moneda) ? moneda : null;
        Fecha = DateTime.TryParseExact(
            Raiz.Valor("Fecha"), FormatoDeFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha
            : null;
        FechaDeVigencia = Fecha is { } f ? DateOnly.FromDateTime(f) : DateOnly.FromDateTime(ahora.UtcDateTime);
        CodigoPostalDeExpedicion = Raiz.Valor("LugarExpedicion") is { } cp && codigosPostales.TryGetValue(cp, out var registro)
            ? registro
            : null;
        TieneComercioExterior = Raiz.Hijo("Complemento")?.Elements()
            .Any(e => e.Name.NamespaceName.StartsWith(EspacioComercioExterior, StringComparison.Ordinal)) ?? false;
        Conceptos = Raiz.Hijo("Conceptos")?.Hijos("Concepto").ToList() ?? [];
    }

    public XElement Raiz { get; }

    public XElement? Emisor => Raiz.Hijo("Emisor");

    public XElement? Receptor => Raiz.Hijo("Receptor");

    public IReadOnlyList<XElement> Conceptos { get; }

    public CatalogosPequenos Catalogos { get; }

    public IReadOnlyDictionary<string, ClaveProdServ> ClavesProdServ { get; }

    public IReadOnlyDictionary<string, ClaveUnidad> ClavesUnidad { get; }

    public IReadOnlyDictionary<string, CodigoPostal> CodigosPostales { get; }

    public DateTimeOffset Ahora { get; }

    /// <summary>I, E, T, N o P tal como viene (y sin revisar: de eso se encarga el catálogo).</summary>
    public string? Tipo { get; }

    /// <summary>La moneda si está en el catálogo; sin ella no se puede saber cuántos decimales admiten los importes.</summary>
    public Moneda? Moneda { get; }

    public int? DecimalesMoneda => Moneda?.Decimales;

    /// <summary>La fecha del comprobante si es una fecha y hora real con el formato del esquema.</summary>
    public DateTime? Fecha { get; }

    /// <summary>La fecha con la que se revisa la vigencia de los catálogos: la del comprobante o, si no se puede leer, hoy.</summary>
    public DateOnly FechaDeVigencia { get; }

    public CodigoPostal? CodigoPostalDeExpedicion { get; }

    public bool TieneComercioExterior { get; }

    public bool EsIngresoEgresoONomina => Tipo is "I" or "E" or "N";

    public IReadOnlyList<Hallazgo> Hallazgos => _hallazgos;

    /// <summary>Cuántos hallazgos se dejaron de guardar por pasar de <see cref="MaximoDeHallazgos"/>.</summary>
    public int Omitidos { get; private set; }

    public void Error(string codigo, XElement nodo, string? atributo, string mensaje, string? encontrado = null, string? esperado = null) =>
        Agregar(codigo, Severidad.Error, nodo, atributo, mensaje, encontrado, esperado);

    public void Advertencia(string codigo, XElement nodo, string? atributo, string mensaje, string? encontrado = null, string? esperado = null) =>
        Agregar(codigo, Severidad.Advertencia, nodo, atributo, mensaje, encontrado, esperado);

    private void Agregar(string codigo, Severidad severidad, XElement nodo, string? atributo, string mensaje, string? encontrado, string? esperado)
    {
        if (_hallazgos.Count >= MaximoDeHallazgos)
        {
            Omitidos++;
            return;
        }

        XObject origen = atributo is not null && nodo.Attribute(atributo) is { } a ? a : nodo;
        var informacion = (IXmlLineInfo)origen;
        _hallazgos.Add(new Hallazgo(
            codigo,
            severidad,
            RutaXml.De(nodo),
            atributo,
            mensaje,
            encontrado,
            esperado,
            informacion.HasLineInfo() ? informacion.LineNumber : null));
    }

    /// <summary>
    /// Revisa que la clave de un atributo esté en un catálogo y siga vigente en la fecha del comprobante. Devuelve verdadero si
    /// es así; si no, anota el hallazgo con el código del SAT de ese catálogo. Un atributo ausente no se revisa aquí (de
    /// eso se ocupa el esquema o una regla propia).
    /// </summary>
    public bool EnCatalogo<T>(
        IReadOnlyDictionary<string, T> catalogo, string codigo, XElement nodo, string atributo, string nombreCatalogo)
        where T : IVigente
    {
        var clave = nodo.Valor(atributo);
        if (clave is null)
        {
            return false;
        }

        if (!catalogo.TryGetValue(clave, out var registro))
        {
            Error(codigo, nodo, atributo, $"La clave {clave} no está en el catálogo {nombreCatalogo} del SAT.", clave);
            return false;
        }

        if (!registro.VigenteEn(FechaDeVigencia))
        {
            var hasta = registro.FinVigencia is { } fin ? $" hasta el {fin:yyyy-MM-dd}" : string.Empty;
            Error(
                codigo,
                nodo,
                atributo,
                $"La clave {clave} del catálogo {nombreCatalogo} no estaba vigente el {FechaDeVigencia:yyyy-MM-dd}: vale desde el {registro.InicioVigencia:yyyy-MM-dd}{hasta}.",
                clave);
            return false;
        }

        return true;
    }

    /// <summary>Anota el hallazgo si el atributo trae más decimales de los que admite la moneda del comprobante.</summary>
    public void DecimalesDeLaMoneda(string codigo, XElement nodo, string atributo)
    {
        if (DecimalesMoneda is not { } permitidos || !nodo.Tiene(atributo))
        {
            return;
        }

        var escritos = nodo.Decimales(atributo);
        if (escritos > permitidos)
        {
            Error(
                codigo,
                nodo,
                atributo,
                $"{atributo} trae {escritos} decimales y la moneda {Moneda!.Clave} admite {permitidos}.",
                nodo.Valor(atributo));
        }
    }

    /// <summary>Persona moral (RFC de 12 caracteres) o física (13); nulo si el RFC no tiene ninguna de las dos longitudes.</summary>
    public static bool? EsPersonaMoral(string? rfc) => rfc?.Length switch
    {
        12 => true,
        13 => false,
        _ => null,
    };

    public static bool EsRfcGenerico(string? rfc) => rfc is RfcGenericoNacional or RfcGenericoExtranjero;
}
