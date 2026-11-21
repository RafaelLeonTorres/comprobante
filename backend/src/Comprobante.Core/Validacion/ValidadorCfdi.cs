using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Comprobante.Core.Catalogos;
using Comprobante.Core.Sello;
using Comprobante.Core.Xml;

namespace Comprobante.Core.Validacion;

/// <summary>
/// Revisa un XML de CFDI 4.0 y devuelve un reporte con cada hallazgo. Va por capas y se detiene cuando una impide seguir
/// (XML mal formado, otra versión): lectura segura, esquema (XSD) y el sello contra la cadena original.
/// </summary>
public sealed class ValidadorCfdi(ICatalogos catalogos)
{
    private const string CodigoSello = "CFDI40102";
    private const string CodigoCertificado = "CFDI40106";

    /// <summary>Los catálogos con los que se revisan las claves: los usan las reglas de la matriz de errores (fase 3).</summary>
    internal ICatalogos Catalogos { get; } = catalogos;

    public Task<ReporteValidacion> ValidarAsync(string xml, CancellationToken cancelacion = default)
    {
        cancelacion.ThrowIfCancellationRequested();
        return Task.FromResult(Validar(xml));
    }

    private static ReporteValidacion Validar(string xml)
    {
        var hallazgos = new List<Hallazgo>();

        XDocument documento;
        try
        {
            documento = LectorXmlSeguro.Cargar(xml);
        }
        catch (XmlException ex)
        {
            hallazgos.Add(new Hallazgo(
                Codigos.XmlNoLegible,
                Severidad.Error,
                string.Empty,
                null,
                $"El XML no se pudo leer: {ex.Message}",
                Linea: ex.LineNumber > 0 ? ex.LineNumber : null));
            return ReporteValidacion.De(hallazgos, null, null, null);
        }

        var raiz = documento.Root!;
        if (raiz.Name != XName.Get("Comprobante", EscritorCfdi.EspacioCfdi))
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

        hallazgos.AddRange(ValidadorXsd.Validar(documento));

        var (cadena, selloValido, certificado) = RevisarSello(documento, raiz, hallazgos);
        return ReporteValidacion.De(hallazgos, cadena, selloValido, certificado);
    }

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
                $"No se pudo calcular la cadena original: {ex.Message}"));
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
                "El certificado no cumple con alguno de los valores permitidos: el atributo debe contener un archivo .cer válido."));
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
                "El resultado de la digestión debe ser igual al resultado de la desencripción del sello: el sello no corresponde a la cadena original de este XML con el certificado que trae."));
        }

        return (cadena, valido, datos);
    }
}
