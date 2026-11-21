using System.Xml.Linq;
using Comprobante.Core.Xml;

namespace Comprobante.UnitTests.Xml;

public class CadenaOriginalTests
{
    public static TheoryData<string> Escenarios() => Referencia.Escenarios();

    [Theory]
    [MemberData(nameof(Escenarios))]
    public void La_cadena_con_System_Xml_es_la_misma_que_saca_libxslt_con_el_XSLT_del_SAT(string escenario)
    {
        // Los XSLT del SAT dicen version="2.0"; XslCompiledTransform es 1.0. Esta prueba comprueba que, con los XSLT oficiales,
        // los dos motores dan el mismo texto en comprobantes con acentos, &, Ñ, retenciones, exentos y relacionados.
        var documento = XDocument.Parse(Referencia.Xml(escenario));

        Assert.Equal(Referencia.Cadena(escenario), CadenaOriginal.Generar(documento));
    }

    [Fact]
    public void La_cadena_sigue_el_orden_del_esquema_y_no_el_de_los_atributos_del_XML()
    {
        // Mismos valores, atributos en otro orden: la cadena es la misma porque el XSLT los recorre en el orden del Anexo 20.
        const string ordenado =
            """
            <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" Version="4.0" Fecha="2026-11-21T09:30:00" NoCertificado="30001000000500003416" SubTotal="100.00" Moneda="MXN" Total="100.00" TipoDeComprobante="I" Exportacion="01" LugarExpedicion="42501">
              <cfdi:Emisor Rfc="EKU9003173C9" Nombre="ESCUELA KEMPER URGATE" RegimenFiscal="601"/>
              <cfdi:Receptor Rfc="XAXX010101000" Nombre="PUBLICO EN GENERAL" DomicilioFiscalReceptor="42501" RegimenFiscalReceptor="616" UsoCFDI="S01"/>
              <cfdi:Conceptos/>
            </cfdi:Comprobante>
            """;
        const string desordenado =
            """
            <cfdi:Comprobante LugarExpedicion="42501" Exportacion="01" TipoDeComprobante="I" Total="100.00" Moneda="MXN" SubTotal="100.00" NoCertificado="30001000000500003416" Fecha="2026-11-21T09:30:00" Version="4.0" xmlns:cfdi="http://www.sat.gob.mx/cfd/4">
              <cfdi:Emisor RegimenFiscal="601" Nombre="ESCUELA KEMPER URGATE" Rfc="EKU9003173C9"/>
              <cfdi:Receptor UsoCFDI="S01" RegimenFiscalReceptor="616" DomicilioFiscalReceptor="42501" Nombre="PUBLICO EN GENERAL" Rfc="XAXX010101000"/>
              <cfdi:Conceptos/>
            </cfdi:Comprobante>
            """;

        var esperada =
            "||4.0|2026-11-21T09:30:00|30001000000500003416|100.00|MXN|100.00|I|01|42501"
            + "|EKU9003173C9|ESCUELA KEMPER URGATE|601"
            + "|XAXX010101000|PUBLICO EN GENERAL|42501|616|S01||";

        Assert.Equal(esperada, CadenaOriginal.Generar(XDocument.Parse(ordenado)));
        Assert.Equal(esperada, CadenaOriginal.Generar(XDocument.Parse(desordenado)));
    }

    [Fact]
    public void Los_espacios_de_sobra_se_normalizan_y_los_atributos_omitidos_no_dejan_barras()
    {
        const string xml =
            """
            <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" Version="4.0" Fecha="2026-11-21T09:30:00" NoCertificado="30001000000500003416" SubTotal="1.00" Moneda="MXN" Total="1.00" TipoDeComprobante="I" Exportacion="01" LugarExpedicion="42501">
              <cfdi:Emisor Rfc="EKU9003173C9" Nombre="  ESCUELA    KEMPER   URGATE  " RegimenFiscal="601"/>
              <cfdi:Receptor Rfc="XAXX010101000" Nombre="PUBLICO EN GENERAL" DomicilioFiscalReceptor="42501" RegimenFiscalReceptor="616" UsoCFDI="S01"/>
              <cfdi:Conceptos/>
            </cfdi:Comprobante>
            """;

        var cadena = CadenaOriginal.Generar(XDocument.Parse(xml));

        Assert.Contains("|ESCUELA KEMPER URGATE|", cadena, StringComparison.Ordinal);
        Assert.DoesNotContain("||EKU", cadena, StringComparison.Ordinal);
        Assert.StartsWith("||4.0|2026-11-21T09:30:00|30001000000500003416|1.00|MXN|1.00|I|01|42501|EKU9003173C9|", cadena, StringComparison.Ordinal);
        Assert.EndsWith("||", cadena, StringComparison.Ordinal);
    }

    [Fact]
    public void El_XSLT_y_sus_includes_se_resuelven_solo_con_los_archivos_del_SAT()
    {
        Assert.Equal("sat/xslt/utilerias.xslt", ResolverSat.NombreDelRecurso(
            new Uri("http://www.sat.gob.mx/sitio_internet/cfd/4/cadenaoriginal_4_0/utilerias.xslt")));
        Assert.Equal("sat/esquemas/catCFDI.xsd", ResolverSat.NombreDelRecurso(
            new Uri("http://www.sat.gob.mx/sitio_internet/cfd/catalogos/catCFDI.xsd")));

        // Cualquier otra cosa: otro servidor, un archivo local, una extensión que no es XSD/XSLT, o un archivo que no viaja.
        Assert.Null(ResolverSat.NombreDelRecurso(new Uri("http://evil.example.com/sitio_internet/cfd/catalogos/catCFDI.xsd")));
        Assert.Null(ResolverSat.NombreDelRecurso(new Uri("file:///etc/passwd")));
        Assert.Null(ResolverSat.NombreDelRecurso(new Uri("http://www.sat.gob.mx/sitio_internet/cfd/4/secreto.xml")));
        Assert.Null(ResolverSat.NombreDelRecurso(new Uri("http://www.sat.gob.mx/sitio_internet/cfd/4/no-existe.xslt")));
    }
}
