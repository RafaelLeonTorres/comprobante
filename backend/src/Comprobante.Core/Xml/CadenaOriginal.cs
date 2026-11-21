using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;

namespace Comprobante.Core.Xml;

/// <summary>
/// La cadena original de un CFDI 4.0: el texto que se firma para obtener el sello. Se calcula con el XSLT oficial del SAT
/// (<c>cadenaoriginal_4_0.xslt</c> con todos los XSLT que incluye), que viaja en el ensamblado, así que lo que se firma es
/// exactamente lo que el SAT definió.
/// </summary>
public static class CadenaOriginal
{
    private const string DireccionDelXslt = ResolverSat.DireccionBase + "cfd/4/cadenaoriginal_4_0/cadenaoriginal_4_0.xslt";

    private static readonly Lazy<XslCompiledTransform> Transformacion = new(Cargar, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>La cadena original de este XML, con las barras: <c>||4.0|A|1|2026-...||</c>.</summary>
    public static string Generar(XDocument documento)
    {
        using var entrada = documento.CreateReader();
        using var salida = new StringWriter(CultureInfo.InvariantCulture);
        Transformacion.Value.Transform(entrada, null, salida);
        return salida.ToString();
    }

    private static XslCompiledTransform Cargar()
    {
        // Los XSLT del SAT se declaran como 2.0, pero usan lo que también existe en 1.0 (normalize-space, concat...),
        // que es lo que implementa XslCompiledTransform. Las pruebas lo comprueban con cadenas calculadas aparte.
        using var flujo = RecursosSat.Abrir("sat/xslt/cadenaoriginal_4_0.xslt");
        using var lector = XmlReader.Create(
            flujo,
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null },
            DireccionDelXslt);
        var transformacion = new XslCompiledTransform();
        transformacion.Load(lector, new XsltSettings(enableDocumentFunction: false, enableScript: false), new ResolverSat());
        return transformacion;
    }
}
