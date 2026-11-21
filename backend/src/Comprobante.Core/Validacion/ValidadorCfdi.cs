using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Comprobante.Core.Catalogos;
using Comprobante.Core.Sello;
using Comprobante.Core.Validacion.Reglas;
using Comprobante.Core.Xml;

namespace Comprobante.Core.Validacion;

/// <summary>
/// Revisa un XML de CFDI 4.0 y devuelve un reporte con cada hallazgo. Va por capas y se detiene cuando una impide seguir
/// (XML mal formado, otra versión): lectura segura, esquema (XSD), las reglas de la matriz de errores del SAT que se pueden
/// revisar sin conexión (<see cref="ReglasSat"/>) y, por último, el sello contra la cadena original.
/// </summary>
/// <param name="catalogos">De dónde salen los catálogos del SAT con los que se revisan las claves.</param>
/// <param name="reloj">La hora actual, para avisar de las fechas que todavía no llegan.</param>
public sealed class ValidadorCfdi(ICatalogos catalogos, TimeProvider reloj)
{
    private const string CodigoSello = "CFDI40102";
    private const string CodigoCertificado = "CFDI40106";

    /// <summary>Un identificador de catálogo más largo que esto no puede existir: no se manda a la base de datos.</summary>
    private const int LongitudMaximaDeClave = 20;

    private static readonly XNamespace Cfdi = EscritorCfdi.EspacioCfdi;

    public async Task<ReporteValidacion> ValidarAsync(string xml, CancellationToken cancelacion = default)
    {
        cancelacion.ThrowIfCancellationRequested();
        var hallazgos = new List<Hallazgo>();

        XDocument documento;
        try
        {
            documento = LectorXmlSeguro.Cargar(xml);
        }
        catch (XmlException ex)
        {
            hallazgos.Add(TraductorXml.Traducir(ex));
            return ReporteValidacion.De(hallazgos, null, null, null);
        }

        var raiz = documento.Root!;
        if (raiz.Name != Cfdi + "Comprobante")
        {
            hallazgos.Add(new Hallazgo(
                Codigos.NoEsCfdi40,
                Severidad.Error,
                RutaXml.De(raiz),
                null,
                $"El nodo raíz debe ser Comprobante del espacio de nombres {EscritorCfdi.EspacioCfdi}: este XML no es un CFDI 4.0.",
                Encontrado: raiz.Name.ToString()));
            return ReporteValidacion.De(hallazgos, null, null, null);
        }

        var version = raiz.Attribute("Version")?.Value;
        if (version != "4.0")
        {
            hallazgos.Add(new Hallazgo(
                Codigos.NoEsCfdi40,
                Severidad.Error,
                RutaXml.De(raiz),
                "Version",
                "Solo se revisan comprobantes CFDI 4.0: el atributo Version debe ser 4.0.",
                Encontrado: version,
                Esperado: "4.0"));
            return ReporteValidacion.De(hallazgos, null, null, null);
        }

        var esquema = ValidadorXsd.Validar(documento);
        var reglas = await RevisarReglasAsync(documento, cancelacion);

        // Cuando una regla del SAT señala el mismo atributo que el esquema (por ejemplo, una clave que no está en el
        // catálogo), el hallazgo con el código del SAT dice más que el del esquema: se deja solo ese.
        var cubiertos = reglas
            .Where(h => h.Atributo is not null)
            .Select(h => (h.Nodo, h.Atributo))
            .ToHashSet();
        hallazgos.AddRange(esquema.Where(h => h.Atributo is null || !cubiertos.Contains((h.Nodo, h.Atributo))));
        hallazgos.AddRange(reglas);

        var (cadena, selloValido, certificado) = RevisarSello(documento, raiz, hallazgos);
        return ReporteValidacion.De(hallazgos, cadena, selloValido, certificado);
    }

    /// <summary>Trae los catálogos que el comprobante usa y corre las reglas de la matriz de errores.</summary>
    private async Task<IReadOnlyList<Hallazgo>> RevisarReglasAsync(XDocument documento, CancellationToken cancelacion)
    {
        var raiz = documento.Root!;
        var conceptos = raiz.Element(Cfdi + "Conceptos")?.Elements(Cfdi + "Concepto").ToList() ?? [];
        var partes = conceptos.SelectMany(c => c.Elements(Cfdi + "Parte"));

        var clavesProdServ = Claves(conceptos.Concat(partes), "ClaveProdServ");
        var clavesUnidad = Claves(conceptos, "ClaveUnidad");
        var codigosPostales = Claves([raiz], "LugarExpedicion");

        var pequenos = catalogos.PequenosAsync(cancelacion);
        var productos = catalogos.ClavesProdServAsync(clavesProdServ, cancelacion);
        var unidades = catalogos.ClavesUnidadAsync(clavesUnidad, cancelacion);
        var postales = catalogos.CodigosPostalesAsync(codigosPostales, cancelacion);
        await Task.WhenAll(pequenos, productos, unidades, postales);

        var contexto = new ContextoValidacion(
            documento, pequenos.Result, productos.Result, unidades.Result, postales.Result, reloj.GetUtcNow());
        var hallazgos = MotorDeReglas.Revisar(contexto).ToList();

        if (contexto.Omitidos > 0)
        {
            hallazgos.Add(new Hallazgo(
                Codigos.ReglasOmitidas,
                Severidad.Error,
                RutaXml.De(raiz),
                null,
                $"Hay {contexto.Omitidos} hallazgos más de las reglas del SAT; se muestran solo los primeros {ContextoValidacion.MaximoDeHallazgos}."));
        }

        return hallazgos;
    }

    private static List<string> Claves(IEnumerable<XElement> nodos, string atributo) =>
        nodos
            .Select(n => n.Attribute(atributo)?.Value)
            .OfType<string>()
            .Where(clave => clave.Length is > 0 and <= LongitudMaximaDeClave)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static (string? Cadena, bool? SelloValido, DatosCertificado? Certificado) RevisarSello(
        XDocument documento, XElement raiz, List<Hallazgo> hallazgos)
    {
        string? cadena = null;
        try
        {
            cadena = CadenaOriginal.Generar(documento);
        }
        catch (Exception ex) when (ex is XsltException or XmlException)
        {
            hallazgos.Add(new Hallazgo(
                Codigos.CadenaOriginalNoCalculable,
                Severidad.Error,
                RutaXml.De(raiz),
                null,
                $"No se pudo calcular la cadena original: {ex.Message}",
                Linea: raiz.Linea()));
        }

        var certificadoTexto = raiz.Attribute("Certificado")?.Value;
        if (string.IsNullOrWhiteSpace(certificadoTexto))
        {
            return (cadena, null, null);
        }

        using var certificado = Csd.LeerCertificado(certificadoTexto);
        if (certificado is null)
        {
            hallazgos.Add(new Hallazgo(
                CodigoCertificado,
                Severidad.Error,
                RutaXml.De(raiz),
                "Certificado",
                "El certificado no cumple con alguno de los valores permitidos: el atributo debe contener un archivo .cer válido.",
                Linea: raiz.Attribute("Certificado").Linea()));
            return (cadena, null, null);
        }

        var datos = Csd.DatosDe(certificado);
        var sello = raiz.Attribute("Sello")?.Value;
        if (cadena is null || string.IsNullOrWhiteSpace(sello))
        {
            return (cadena, null, datos);
        }

        var valido = Csd.Verificar(certificado, cadena, sello);
        if (!valido)
        {
            hallazgos.Add(new Hallazgo(
                CodigoSello,
                Severidad.Error,
                RutaXml.De(raiz),
                "Sello",
                "El resultado de la digestión debe ser igual al resultado de la desencripción del sello: el sello no corresponde a la cadena original de este XML con el certificado que trae.",
                Linea: raiz.Attribute("Sello").Linea()));
        }

        return (cadena, valido, datos);
    }
}
