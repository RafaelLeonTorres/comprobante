using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace Comprobante.UnitTests.Pdf;

/// <summary>Comprobantes de ejemplo para probar el PDF (con y sin timbre, largos, raros) y atajos para leer lo que dibujó.</summary>
internal static partial class EjemplosDePdf
{
    public static readonly XNamespace Cfdi = "http://www.sat.gob.mx/cfd/4";
    public static readonly XNamespace Tfd = "http://www.sat.gob.mx/TimbreFiscalDigital";

    public const string UuidDePrueba = "7B2C9E54-0D3A-4F6B-9A11-C85E1D40A2F7";

    /// <summary>Un sello del SAT inventado pero con forma de sello: 344 caracteres en base 64.</summary>
    public static readonly string SelloSatDePrueba = Convert.ToBase64String([.. Enumerable.Range(0, 256).Select(i => (byte)i)]);

    public static XDocument SinTimbre(string escenario = "ingreso-retenciones") => XDocument.Parse(Referencia.Xml(escenario));

    public static XDocument ConTimbre(string escenario = "ingreso-retenciones") => ConTimbre(SinTimbre(escenario));

    /// <summary>Le agrega el timbre fiscal digital con el UUID de prueba, como si ya lo hubiera timbrado un proveedor.</summary>
    public static XDocument ConTimbre(XDocument documento)
    {
        var raiz = documento.Root!;
        var complemento = raiz.Element(Cfdi + "Complemento");
        if (complemento is null)
        {
            complemento = new XElement(Cfdi + "Complemento");
            raiz.Add(complemento);
        }

        complemento.Add(new XElement(
            Tfd + "TimbreFiscalDigital",
            new XAttribute(XNamespace.Xmlns + "tfd", Tfd.NamespaceName),
            new XAttribute("Version", "1.1"),
            new XAttribute("UUID", UuidDePrueba),
            new XAttribute("FechaTimbrado", "2026-11-20T18:05:12"),
            new XAttribute("RfcProvCertif", "SPR190613I52"),
            new XAttribute("SelloCFD", raiz.Attribute("Sello")!.Value),
            new XAttribute("NoCertificadoSAT", "30001000000500003456"),
            new XAttribute("SelloSAT", SelloSatDePrueba)));
        return documento;
    }

    /// <summary>El comprobante con <paramref name="cuantos"/> conceptos, algunos con número de identificación y descripciones de distinto largo.</summary>
    public static XDocument ConMuchosConceptos(int cuantos, string escenario = "ingreso-retenciones")
    {
        var documento = SinTimbre(escenario);
        var conceptos = documento.Descendants(Cfdi + "Conceptos").Single();
        var modelo = conceptos.Elements(Cfdi + "Concepto").First();
        conceptos.RemoveNodes();
        for (var i = 1; i <= cuantos; i++)
        {
            var copia = new XElement(modelo);
            copia.SetAttributeValue("Descripcion", $"Concepto número {i}: " + string.Concat(Enumerable.Repeat("servicio profesional de consultoría ", 1 + (i % 4))).Trim());
            if (i % 3 == 0)
            {
                copia.SetAttributeValue("NoIdentificacion", $"SKU-{i:0000}");
            }

            conceptos.Add(copia);
        }

        return documento;
    }

    /// <summary>Un traslado de IEPS distinto por cada <paramref name="i"/>, para llenar resúmenes de impuestos.</summary>
    public static XElement Traslado(int i) =>
        new(
            Cfdi + "Traslado",
            new XAttribute("Base", $"{1000 + i}.00"),
            new XAttribute("Impuesto", "003"),
            new XAttribute("TipoFactor", "Tasa"),
            new XAttribute("TasaOCuota", $"0.{i:000000}"),
            new XAttribute("Importe", $"{i}.00"));

    public static byte[] Logo(string archivo) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Pdf", "Recursos", archivo));

    /// <summary>El texto de todas las hojas, tal como lo lee un lector de PDF.</summary>
    public static string Texto(byte[] pdf)
    {
        using var documento = PdfDocument.Open(pdf);
        return string.Concat(documento.GetPages().Select(pagina => pagina.Text));
    }

    /// <summary>Quita los espacios y saltos: un texto que se parte en renglones sigue siendo el mismo texto.</summary>
    public static string SinEspacios(string texto) => EspaciosEnBlanco().Replace(texto, string.Empty);

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosEnBlanco();
}
