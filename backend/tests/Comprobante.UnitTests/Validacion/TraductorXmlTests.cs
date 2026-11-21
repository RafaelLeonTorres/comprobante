using Comprobante.Catalogos;
using Comprobante.Core.Validacion;
using Comprobante.UnitTests.Generacion;

namespace Comprobante.UnitTests.Validacion;

/// <summary>
/// Un XML que no se puede ni leer se explica en español con la causa y la línea. Los textos de .NET que se reconocen son los
/// reales: cada caso arma el XML roto, lo pasa por el validador y mira lo que sale.
/// </summary>
public class TraductorXmlTests
{
    private readonly ValidadorCfdi _validador = Servicios.Validador(new CatalogosEnMemoria());

    public static TheoryData<string, string> XmlRotos() => new()
    {
        { "", "no tiene un elemento raíz" },
        { "   ", "no tiene un elemento raíz" },
        { "texto suelto", "datos inválidos fuera del elemento raíz" },
        { "<a>texto</a>basura", "datos inválidos fuera del elemento raíz" },
        { "<a><b>", "faltan por cerrar los elementos b, a" },
        { "<a><b/>", "faltan por cerrar los elementos a" },
        { "<a><b></a>", "no corresponde con la de apertura <b>" },
        { "<a/></a>", "etiqueta de cierre que no tiene su etiqueta de apertura" },
        { "<a/><b/>", "más de un elemento raíz" },
        { "<a b=\"1\" b=\"2\"/>", "el atributo b está repetido" },
        { "<a b=1/>", "debe ir entre comillas" },
        { "<a b=\"1\"c=\"2\"/>", "falta un espacio antes de «c»" },
        { "<a>&nbsp;</a>", "entidad &nbsp;" },
        { "<a>&</a>", "un & suelto" },
        { "<a b=\"x<y\"/>", "no puede ir en el valor de un atributo" },
        { "<a:b xmlns:c=\"x\"/>", "el prefijo «a:» no está declarado" },
        { "<!DOCTYPE a [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><a>&x;</a>", "DOCTYPE" },
        { "<a><!-- uno -- dos --></a>", "dos guiones seguidos" },
        { "<a>\u0001</a>", "carácter que no es válido en XML" },
        { " <?xml version=\"1.0\"?><a/>", "declaración <?xml" },
        { "<a><1b/></a>", "no puede ir en un nombre" },
    };

    [Theory]
    [MemberData(nameof(XmlRotos))]
    public async Task Un_xml_mal_formado_se_explica_en_espanol(string xml, string fragmento)
    {
        var reporte = await _validador.ValidarAsync(xml);

        var hallazgo = Assert.Single(reporte.Hallazgos);
        Assert.Equal(Codigos.XmlNoLegible, hallazgo.Codigo);
        Assert.Equal(Severidad.Error, hallazgo.Severidad);
        Assert.StartsWith("El XML no se pudo leer: ", hallazgo.Mensaje, StringComparison.Ordinal);
        Assert.Contains(fragmento, hallazgo.Mensaje, StringComparison.Ordinal);
        Assert.False(reporte.EsValido);
        Assert.Null(reporte.CadenaOriginal);
        Assert.Null(reporte.SelloValido);
    }

    [Fact]
    public async Task El_motivo_trae_la_linea_donde_se_rompe_el_xml()
    {
        var reporte = await _validador.ValidarAsync("<a>\n  <b>\n  </c>\n</a>");

        var hallazgo = Assert.Single(reporte.Hallazgos);
        Assert.Equal(3, hallazgo.Linea);
        Assert.Contains("<b>", hallazgo.Mensaje, StringComparison.Ordinal);
        Assert.Contains("línea 2", hallazgo.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_xml_de_mas_de_2_MB_no_se_lee()
    {
        var enorme = "<a>" + new string('x', 2 * 1024 * 1024 + 10) + "</a>";

        var reporte = await _validador.ValidarAsync(enorme);

        var hallazgo = Assert.Single(reporte.Hallazgos);
        Assert.Equal(Codigos.XmlNoLegible, hallazgo.Codigo);
        Assert.Contains("más grande de lo permitido", hallazgo.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_xml_que_no_es_un_cfdi_no_pasa_a_las_demas_revisiones()
    {
        var reporte = await _validador.ValidarAsync("<factura><total>10</total></factura>");

        var hallazgo = Assert.Single(reporte.Hallazgos);
        Assert.Equal(Codigos.NoEsCfdi40, hallazgo.Codigo);
    }

    [Fact]
    public async Task Un_cfdi_de_otra_version_se_rechaza_con_el_valor_esperado()
    {
        var reporte = await _validador.ValidarAsync(
            "<cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/4\" Version=\"3.3\"/>");

        var hallazgo = Assert.Single(reporte.Hallazgos);
        Assert.Equal(Codigos.NoEsCfdi40, hallazgo.Codigo);
        Assert.Equal("Version", hallazgo.Atributo);
        Assert.Equal("3.3", hallazgo.Encontrado);
        Assert.Equal("4.0", hallazgo.Esperado);
    }
}
