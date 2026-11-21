using System.Xml.Schema;

namespace Comprobante.Core.Xml;

/// <summary>
/// Los esquemas del CFDI 4.0 (cfdv40.xsd con catCFDI.xsd y tdCFDI.xsd) y el del Timbre Fiscal Digital, compilados una sola
/// vez. catCFDI.xsd pesa 6 MB: compilarlo tarda un par de segundos, así que se hace la primera vez que se pide, no al arrancar.
/// </summary>
internal static class EsquemasCfdi
{
    private static readonly Lazy<XmlSchemaSet> Cargado = new(Crear, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Compilado y de solo lectura: se puede usar desde varios hilos a la vez.</summary>
    public static XmlSchemaSet Conjunto => Cargado.Value;

    private static XmlSchemaSet Crear()
    {
        var conjunto = new XmlSchemaSet { XmlResolver = new ResolverSat() };
        conjunto.Add("http://www.sat.gob.mx/cfd/4", ResolverSat.DireccionBase + "cfd/4/cfdv40.xsd");
        conjunto.Add(
            "http://www.sat.gob.mx/TimbreFiscalDigital",
            ResolverSat.DireccionBase + "cfd/TimbreFiscalDigital/TimbreFiscalDigitalv11.xsd");
        conjunto.Compile();
        return conjunto;
    }
}
