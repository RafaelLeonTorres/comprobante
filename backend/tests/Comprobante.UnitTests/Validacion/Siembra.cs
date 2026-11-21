using System.Globalization;
using System.Xml.Linq;
using Comprobante.Core.Sello;
using Comprobante.Core.Validacion;
using Comprobante.Core.Xml;

namespace Comprobante.UnitTests.Validacion;

/// <summary>
/// Para probar una regla del SAT se parte de un XML correcto, se le siembra un solo error y se vuelve a firmar con el CSD de
/// prueba de su emisor: así el sello no estorba y lo único que puede fallar es lo sembrado (y lo que de ello se desprende).
/// </summary>
internal static class Siembra
{
    public static readonly XNamespace Cfdi = EscritorCfdi.EspacioCfdi;

    private static readonly XNamespace ComercioExterior = "http://www.sat.gob.mx/ComercioExterior20";

    private static readonly EmisoresDePrueba Emisores = new();

    private static readonly Lazy<Dictionary<string, Csd>> CsdPorNumero = new(() =>
        Emisores.Todos
            .Select(e => Emisores.ObtenerCsd(e.Rfc)!)
            .ToDictionary(csd => csd.NoCertificado));

    /// <summary>Rutas de los nodos que más se tocan, relativas a la raíz (ver <see cref="Nodo"/>).</summary>
    public const string Concepto = "Conceptos/Concepto";

    public const string TrasladoDeConcepto = "Conceptos/Concepto/Impuestos/Traslados/Traslado";
    public const string TrasladoDelResumen = "Impuestos/Traslados/Traslado";
    public const string RetencionDeConcepto = "Conceptos/Concepto[1]/Impuestos/Retenciones/Retencion";

    /// <summary>
    /// Los puntos de partida de las pruebas: el escenario de referencia y lo que hay que prepararle. Cada uno debe validar sin
    /// un solo hallazgo (una prueba lo comprueba), porque los casos de cada regla parten de ellos.
    /// </summary>
    public static IReadOnlyDictionary<string, (string Escenario, Action<XDocument> Preparar)> Bases { get; } =
        new Dictionary<string, (string, Action<XDocument>)>
        {
            ["ingreso-basico"] = ("ingreso-basico", _ => { }),
            ["ingreso-retenciones"] = ("ingreso-retenciones", _ => { }),
            ["ingreso-dolares"] = ("ingreso-dolares", _ => { }),
            ["publico-en-general"] = ("publico-en-general", _ => { }),
            ["egreso-relacionado"] = ("egreso-relacionado", _ => { }),
            ["traslado"] = ("ingreso-basico", ComoTraslado),
            ["sin-impuestos"] = ("ingreso-basico", SinImpuestos),
            ["receptor-extranjero"] = ("publico-en-general", ComoExtranjero),
        };

    public static XDocument Cargar(string escenario) => XDocument.Parse(Referencia.Xml(escenario));

    /// <summary>
    /// El nodo de una ruta que parte de la raíz, con los nombres sin prefijo y la posición entre corchetes cuando se repite
    /// (empieza en 1): <c>Conceptos/Concepto[2]/Impuestos</c>. La ruta vacía es la raíz.
    /// </summary>
    public static XElement Nodo(this XDocument documento, string ruta = "")
    {
        var actual = documento.Root!;
        foreach (var paso in ruta.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var corchete = paso.IndexOf('[', StringComparison.Ordinal);
            var nombre = corchete < 0 ? paso : paso[..corchete];
            var posicion = corchete < 0 ? 1 : int.Parse(paso[(corchete + 1)..^1], CultureInfo.InvariantCulture);
            actual = actual.Elements(Cfdi + nombre).ElementAtOrDefault(posicion - 1)
                ?? throw new InvalidOperationException($"No hay {paso} en la ruta {ruta}.");
        }

        return actual;
    }

    /// <summary>Pone el valor de un atributo; con <c>null</c> lo quita.</summary>
    public static void Poner(this XDocument documento, string ruta, string atributo, string? valor) =>
        documento.Nodo(ruta).SetAttributeValue(atributo, valor);

    public static void Quitar(this XDocument documento, string ruta) => documento.Nodo(ruta).Remove();

    /// <summary>Calcula la cadena original del XML tal como quedó, la firma con el CSD del certificado que trae y lo devuelve como texto.</summary>
    public static string Firmar(XDocument documento)
    {
        var raiz = documento.Root!;
        var csd = CsdPorNumero.Value[raiz.Attribute("NoCertificado")!.Value];
        raiz.SetAttributeValue("Sello", csd.Firmar(CadenaOriginal.Generar(documento)));
        return documento.ToString(SaveOptions.DisableFormatting);
    }

    public static string Generar(string escenario, Action<XDocument> sembrar)
    {
        var documento = Cargar(escenario);
        sembrar(documento);
        return Firmar(documento);
    }

    public static string Resumen(ReporteValidacion reporte) =>
        string.Join(
            Environment.NewLine,
            reporte.Hallazgos.Select(h =>
                $"  {h.Codigo} [{h.Severidad}] {h.Nodo} @{h.Atributo}: {h.Mensaje} (encontrado: {h.Encontrado}; esperado: {h.Esperado})"));

    // ---- Puntos de partida derivados de los escenarios de referencia ----

    /// <summary>Un comprobante de traslado (tipo T): sin forma ni método de pago, en moneda XXX, con los totales en cero y sin impuestos.</summary>
    public static void ComoTraslado(XDocument d)
    {
        d.Poner("", "TipoDeComprobante", "T");
        d.Poner("", "SubTotal", "0");
        d.Poner("", "Total", "0");
        d.Poner("", "Moneda", "XXX");
        d.Poner("", "FormaPago", null);
        d.Poner("", "MetodoPago", null);
        d.Quitar("Impuestos");
        d.Poner(Concepto, "ObjetoImp", "01");
        d.Quitar("Conceptos/Concepto/Impuestos");
    }

    /// <summary>El ingreso básico sin impuestos: el concepto no es objeto de impuesto y el total es el subtotal.</summary>
    public static void SinImpuestos(XDocument d)
    {
        d.Poner(Concepto, "ObjetoImp", "01");
        d.Quitar("Conceptos/Concepto/Impuestos");
        d.Quitar("Impuestos");
        d.Poner("", "Total", "1000.00");
    }

    /// <summary>La venta a público en general convertida en una a un receptor extranjero: RFC XEXX010101000 con residencia en Estados Unidos.</summary>
    public static void ComoExtranjero(XDocument d)
    {
        d.Quitar("InformacionGlobal");
        d.Poner("Receptor", "Rfc", "XEXX010101000");
        d.Poner("Receptor", "Nombre", "CLIENTE EXTRANJERO");
        d.Poner("Receptor", "ResidenciaFiscal", "USA");
    }

    /// <summary>Agrega el complemento de Comercio Exterior (vacío: solo importa que esté) y, por omisión, la exportación definitiva que le corresponde.</summary>
    public static void ConComercioExterior(XDocument d, bool exportacionDefinitiva = true)
    {
        d.Root!.Add(new XElement(
            Cfdi + "Complemento",
            new XElement(
                ComercioExterior + "ComercioExterior",
                new XAttribute(XNamespace.Xmlns + "cce20", ComercioExterior.NamespaceName),
                new XAttribute("Version", "2.0"))));
        if (exportacionDefinitiva)
        {
            d.Poner("", "Exportacion", "02");
        }
    }

    /// <summary>El IVA del concepto y del resumen pasa a ser un IEPS del 25 %, con sus importes y totales al día.</summary>
    public static void IepsEnLugarDeIva(XDocument d)
    {
        foreach (var ruta in new[] { TrasladoDeConcepto, TrasladoDelResumen })
        {
            d.Poner(ruta, "Impuesto", "003");
            d.Poner(ruta, "TasaOCuota", "0.250000");
            d.Poner(ruta, "Importe", "250.00");
        }

        d.Poner("Impuestos", "TotalImpuestosTrasladados", "250.00");
        d.Poner("", "Total", "1250.00");
    }

    /// <summary>Una tasa de IVA que no existe en el catálogo (17 %) en el concepto y en el resumen, con todo lo demás al día.</summary>
    public static void TasaInvalida(XDocument d)
    {
        foreach (var ruta in new[] { TrasladoDeConcepto, TrasladoDelResumen })
        {
            d.Poner(ruta, "TasaOCuota", "0.170000");
            d.Poner(ruta, "Importe", "170.00");
        }

        d.Poner("Impuestos", "TotalImpuestosTrasladados", "170.00");
        d.Poner("", "Total", "1170.00");
    }

    /// <summary>Agrega el nodo ACuentaTerceros al primer concepto (va justo después de Impuestos).</summary>
    public static void ConTerceros(XDocument d, string rfc, string nombre, string regimen, string domicilio) =>
        d.Nodo("Conceptos/Concepto/Impuestos").AddAfterSelf(new XElement(
            Cfdi + "ACuentaTerceros",
            new XAttribute("RfcACuentaTerceros", rfc),
            new XAttribute("NombreACuentaTerceros", nombre),
            new XAttribute("RegimenFiscalACuentaTerceros", regimen),
            new XAttribute("DomicilioFiscalACuentaTerceros", domicilio)));

    /// <summary>Un número de pedimento con el formato del SAT: 2, 2, 4 y 7 dígitos separados por dos espacios.</summary>
    public static XElement Aduanera() =>
        new(Cfdi + "InformacionAduanera", new XAttribute("NumeroPedimento", "21  47  3807  8003832"));

    /// <summary>Agrega una parte al primer concepto; los importes opcionales se omiten con <c>null</c>.</summary>
    public static void ConParte(
        XDocument d, string claveProdServ, string cantidad, string? valorUnitario, string? importe, bool conAduanera = false) =>
        d.Nodo(Concepto).Add(new XElement(
            Cfdi + "Parte",
            new XAttribute("ClaveProdServ", claveProdServ),
            new XAttribute("Cantidad", cantidad),
            new XAttribute("Descripcion", "Parte de prueba"),
            valorUnitario is null ? null : new XAttribute("ValorUnitario", valorUnitario),
            importe is null ? null : new XAttribute("Importe", importe),
            conAduanera ? Aduanera() : null));
}
