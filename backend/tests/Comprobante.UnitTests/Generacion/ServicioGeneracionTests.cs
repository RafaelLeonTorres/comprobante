using System.Xml.Linq;
using Comprobante.Core.Generacion;
using Comprobante.Core.Sello;
using Comprobante.Core.Solicitud;
using Comprobante.Core.Xml;
using static Comprobante.UnitTests.Datos;

namespace Comprobante.UnitTests.Generacion;

[Collection(ColeccionCatalogos.Nombre)]
public class ServicioGeneracionTests(CatalogosFixture catalogos)
{
    private static readonly XNamespace Cfdi = EscritorCfdi.EspacioCfdi;

    private ServicioGeneracion Servicio(DateTimeOffset? ahora = null) => Servicios.Generacion(catalogos.Catalogos, ahora);

    /// <summary>El comprobante básico de la referencia, sin fecha para que use la hora del reloj.</summary>
    private static SolicitudCfdi Basica() => Referencia.Solicitud("ingreso-basico") with { Fecha = null };

    private async Task<ResultadoGeneracion> Generar(SolicitudCfdi solicitud, DateTimeOffset? ahora = null)
    {
        var resultado = await Servicio(ahora).GenerarAsync(solicitud);
        Assert.True(resultado.EsCorrecto, string.Join("; ", resultado.Errores.Select(e => $"{e.Campo}: {e.Mensaje}")));
        return resultado.Valor;
    }

    /// <summary>Un atributo del comprobante ("Comprobante") o de uno de sus hijos directos (Emisor, Receptor, Impuestos...).</summary>
    private static string Atributo(ResultadoGeneracion resultado, string nodo, string atributo)
    {
        var raiz = XDocument.Parse(resultado.Xml).Root!;
        var elemento = nodo == "Comprobante" ? raiz : raiz.Element(Cfdi + nodo)!;
        return elemento.Attribute(atributo)!.Value;
    }

    [Fact]
    public async Task Sin_fecha_usa_la_hora_actual_del_lugar_de_expedicion()
    {
        var resultado = await Generar(Basica());

        // 15:30 UTC son las 09:30 en el centro del país; el emisor EKU9003173C9 expide desde 42501 (Hidalgo, huso del Centro).
        Assert.Equal("2026-11-21T09:30:00", Atributo(resultado, "Comprobante", "Fecha"));
        Assert.Equal("42501", Atributo(resultado, "Comprobante", "LugarExpedicion"));
    }

    [Theory]
    [InlineData("06600", "2026-11-21T09:30:00")] // Centro
    [InlineData("23000", "2026-11-21T08:30:00")] // Pacífico (La Paz)
    [InlineData("21000", "2026-11-21T07:30:00")] // Noroeste (Mexicali)
    [InlineData("77000", "2026-11-21T10:30:00")] // Sureste (Chetumal)
    [InlineData("88000", "2026-11-21T09:30:00")] // Centro en Frontera (Nuevo Laredo), en noviembre igual que el Centro
    public async Task El_huso_del_codigo_postal_decide_la_hora(string codigoPostal, string esperada)
    {
        var resultado = await Generar(Basica() with { LugarExpedicion = codigoPostal });

        Assert.Equal(esperada, Atributo(resultado, "Comprobante", "Fecha"));
    }

    [Fact]
    public async Task En_la_frontera_el_horario_de_verano_cambia_la_hora()
    {
        var julio = new DateTimeOffset(2026, 7, 15, 15, 30, 0, TimeSpan.Zero);

        var centro = await Generar(Basica() with { LugarExpedicion = "06600" }, julio);
        var frontera = await Generar(Basica() with { LugarExpedicion = "88000" }, julio);

        Assert.Equal("2026-07-15T09:30:00", Atributo(centro, "Comprobante", "Fecha"));
        Assert.Equal("2026-07-15T10:30:00", Atributo(frontera, "Comprobante", "Fecha"));
    }

    [Fact]
    public async Task Con_fecha_indicada_se_respeta_sin_fracciones_de_segundo()
    {
        var resultado = await Generar(Basica() with { Fecha = new DateTime(2026, 11, 20, 18, 5, 9, 999) });

        Assert.Equal("2026-11-20T18:05:09", Atributo(resultado, "Comprobante", "Fecha"));
    }

    [Fact]
    public async Task Sin_fecha_y_con_un_codigo_postal_que_no_existe_el_error_es_del_lugar_de_expedicion()
    {
        var resultado = await Servicio().GenerarAsync(Basica() with { LugarExpedicion = "00000" });

        var error = Assert.Single(resultado.Errores);
        Assert.Equal("lugarExpedicion", error.Campo);
        Assert.Contains("c_CodigoPostal", error.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sin_solicitud_el_error_es_del_cuerpo()
    {
        var resultado = await Servicio().GenerarAsync(null);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal("", error.Campo);
    }

    [Fact]
    public async Task Sin_emisor_se_pide_el_emisor()
    {
        var resultado = await Servicio().GenerarAsync(Basica() with { Emisor = null });

        Assert.Contains(resultado.Errores, e => e.Campo == "emisor");
    }

    [Fact]
    public async Task Un_emisor_sin_RFC_se_senala_en_su_campo()
    {
        var resultado = await Servicio().GenerarAsync(Basica() with { Emisor = new SolicitudEmisor { Nombre = "Alguien" } });

        Assert.Contains(resultado.Errores, e => e.Campo == "emisor.rfc");
    }

    [Fact]
    public async Task Solo_se_firma_con_los_certificados_de_prueba()
    {
        var resultado = await Servicio().GenerarAsync(Basica() with { Emisor = new SolicitudEmisor { Rfc = "xaxx010101000" } });

        var error = Assert.Single(resultado.Errores);
        Assert.Equal("emisor.rfc", error.Campo);
        Assert.Contains("XAXX010101000", error.Mensaje, StringComparison.Ordinal);
        Assert.Contains("certificado de prueba", error.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Los_errores_del_emisor_y_los_del_calculo_se_reportan_juntos()
    {
        var resultado = await Servicio().GenerarAsync(
            Basica() with { Emisor = new SolicitudEmisor { Rfc = "XAXX010101000" }, Conceptos = [] });

        Assert.Contains(resultado.Errores, e => e.Campo == "emisor.rfc");
        Assert.Contains(resultado.Errores, e => e.Campo == "conceptos");
    }

    [Fact]
    public async Task Nombre_regimen_y_lugar_de_expedicion_se_toman_del_emisor_de_prueba_si_se_omiten()
    {
        var resultado = await Generar(Basica() with
        {
            Emisor = new SolicitudEmisor { Rfc = "wATm640917j45" },
            LugarExpedicion = null,
        });

        Assert.Equal("WATM640917J45", Atributo(resultado, "Emisor", "Rfc"));
        Assert.Equal("MARIA WATEMBER TORRES", Atributo(resultado, "Emisor", "Nombre"));
        Assert.Equal("612", Atributo(resultado, "Emisor", "RegimenFiscal"));
        Assert.Equal("43543", Atributo(resultado, "Comprobante", "LugarExpedicion"));
    }

    [Fact]
    public async Task Lo_que_el_usuario_indica_del_emisor_se_respeta_aunque_no_coincida_con_el_registro()
    {
        var resultado = await Generar(Basica() with
        {
            Emisor = new SolicitudEmisor { Rfc = "EKU9003173C9", Nombre = "OTRO NOMBRE", RegimenFiscal = "603" },
            LugarExpedicion = "06600",
        });

        Assert.Equal("OTRO NOMBRE", Atributo(resultado, "Emisor", "Nombre"));
        Assert.Equal("603", Atributo(resultado, "Emisor", "RegimenFiscal"));
        Assert.Equal("06600", Atributo(resultado, "Comprobante", "LugarExpedicion"));
    }

    [Fact]
    public async Task Cadena_sello_y_certificado_del_resultado_son_los_del_XML()
    {
        var resultado = await Generar(Basica());

        var documento = XDocument.Parse(resultado.Xml);
        var raiz = documento.Root!;
        Assert.Equal(resultado.Sello, raiz.Attribute("Sello")!.Value);
        Assert.Equal(resultado.NoCertificado, raiz.Attribute("NoCertificado")!.Value);
        Assert.Equal(CadenaOriginal.Generar(documento), resultado.CadenaOriginal);

        using var certificado = Csd.LeerCertificado(raiz.Attribute("Certificado")!.Value)!;
        Assert.True(Csd.Verificar(certificado, resultado.CadenaOriginal, resultado.Sello));
        Assert.StartsWith("||4.0|", resultado.CadenaOriginal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_XML_va_en_UTF8_sin_marca_de_orden_de_bytes_y_con_saltos_de_linea_LF()
    {
        var resultado = await Generar(Basica());

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<cfdi:Comprobante ", resultado.Xml, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', resultado.Xml);
        Assert.NotEqual('﻿', resultado.Xml[0]);
    }

    [Theory]
    [InlineData("EKU9003173C9", "A", "123", "EKU9003173C9-A-123-prueba.xml")]
    [InlineData("EKU9003173C9", null, "7", "EKU9003173C9-7-prueba.xml")]
    [InlineData("EKU9003173C9", "NC", null, "EKU9003173C9-NC-prueba.xml")]
    [InlineData("EKU9003173C9", null, null, "EKU9003173C9-20261121T093000-prueba.xml")]
    [InlineData("EKU9003173C9", "A/B", "1 2", "EKU9003173C9-A_B-1_2-prueba.xml")]
    [InlineData("H&E951128469", "USD", "7", "H_E951128469-USD-7-prueba.xml")]
    [InlineData("IAÑL750210963", "E", "45", "IA_L750210963-E-45-prueba.xml")]
    public async Task El_nombre_del_archivo_dice_que_es_de_prueba_y_es_seguro(string rfc, string? serie, string? folio, string esperado)
    {
        var resultado = await Generar(Basica() with
        {
            Emisor = new SolicitudEmisor { Rfc = rfc },
            LugarExpedicion = null,
            Serie = serie,
            Folio = folio,
        });

        Assert.Equal(esperado, resultado.NombreArchivo);
    }

    [Fact]
    public async Task Quinientos_conceptos_se_generan_y_pasan_el_esquema()
    {
        var concepto = Concepto(2, 10.5m, traslados: [Iva()]);
        var solicitud = Basica() with { Conceptos = Enumerable.Repeat(concepto, 500).ToList() };

        var resultado = await Generar(solicitud);

        Assert.Equal(500, XDocument.Parse(resultado.Xml).Descendants(Cfdi + "Concepto").Count());
        Assert.True(resultado.Validacion.EsValido);
        Assert.Equal("10500.00", Atributo(resultado, "Comprobante", "SubTotal"));
        Assert.Equal("12180.00", Atributo(resultado, "Comprobante", "Total"));
    }

    [Fact]
    public async Task El_calculo_del_resultado_es_el_que_se_escribio_en_el_XML()
    {
        var resultado = await Generar(Referencia.Solicitud("ingreso-retenciones"));

        Assert.Equal(10708.48m, resultado.Calculo.Total);
        Assert.Equal("10708.48", Atributo(resultado, "Comprobante", "Total"));
        Assert.Equal("11200.48", Atributo(resultado, "Comprobante", "SubTotal"));
        Assert.Equal("100.00", Atributo(resultado, "Comprobante", "Descuento"));
        Assert.Equal("1736.00", Atributo(resultado, "Impuestos", "TotalImpuestosRetenidos"));
    }
}
