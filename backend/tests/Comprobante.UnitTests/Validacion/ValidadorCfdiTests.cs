using System.Text;
using System.Xml.Linq;
using Comprobante.Core.Validacion;
using Comprobante.UnitTests.Generacion;

namespace Comprobante.UnitTests.Validacion;

/// <summary>Las capas del validador que hay desde la fase 2: lectura del XML, esquema (XSD), cadena original y sello.</summary>
[Collection(ColeccionCatalogos.Nombre)]
public class ValidadorCfdiTests(CatalogosFixture catalogos)
{
    private readonly ValidadorCfdi _validador = Servicios.Validador(catalogos.Catalogos);

    private static string Basico() => Referencia.Xml("ingreso-basico");

    private async Task<ReporteValidacion> Validar(string xml) => await _validador.ValidarAsync(xml);

    private static string Resumen(ReporteValidacion reporte) =>
        string.Join("; ", reporte.Hallazgos.Select(h => $"{h.Codigo} {h.Nodo} {h.Atributo}: {h.Mensaje}"));

    [Fact]
    public async Task Un_comprobante_correcto_no_tiene_hallazgos_y_trae_su_cadena_y_su_certificado()
    {
        var reporte = await Validar(Basico());

        Assert.True(reporte.EsValido, Resumen(reporte));
        Assert.Empty(reporte.Hallazgos);
        Assert.True(reporte.SelloValido);
        Assert.Equal(Referencia.Cadena("ingreso-basico"), reporte.CadenaOriginal);
        Assert.Equal("30001000000500003416", reporte.Certificado!.NoCertificado);
        Assert.Equal("EKU9003173C9", reporte.Certificado.Rfc);
    }

    [Fact]
    public async Task Cambiar_un_dato_que_ninguna_regla_revisa_invalida_solo_el_sello()
    {
        // La descripción del concepto no la revisa ninguna regla, pero sí entra en la cadena original.
        var reporte = await Validar(Basico().Replace("Descripcion=\"", "Descripcion=\"(cambiada) ", StringComparison.Ordinal));

        Assert.False(reporte.EsValido);
        Assert.False(reporte.SelloValido);
        var hallazgo = Assert.Single(reporte.Hallazgos);
        Assert.Equal("CFDI40102", hallazgo.Codigo);
        Assert.Equal(Severidad.Error, hallazgo.Severidad);
        Assert.Equal("Sello", hallazgo.Atributo);
        Assert.Equal("cfdi:Comprobante", hallazgo.Nodo);
        Assert.True(hallazgo.Linea > 0);
    }

    [Fact]
    public async Task Cambiar_el_total_se_detecta_por_la_regla_del_total_y_por_el_sello()
    {
        var reporte = await Validar(Basico().Replace("Total=\"1160.00\"", "Total=\"1161.00\"", StringComparison.Ordinal));

        Assert.False(reporte.EsValido);
        Assert.False(reporte.SelloValido);
        Assert.Equal(["CFDI40119", "CFDI40102"], reporte.Hallazgos.Select(h => h.Codigo));
        var total = reporte.Hallazgos[0];
        Assert.Equal("Total", total.Atributo);
        Assert.Equal("1161.00", total.Encontrado);
        Assert.Equal("1160.00", total.Esperado);
    }

    [Fact]
    public async Task Cambiar_el_sello_por_otro_valor_tambien_se_detecta()
    {
        var xml = Basico();
        var sello = Texto(xml, "Sello");

        var reporte = await Validar(xml.Replace(sello, Convert.ToBase64String(new byte[256]), StringComparison.Ordinal));

        Assert.False(reporte.SelloValido);
        Assert.Contains(reporte.Hallazgos, h => h.Codigo == "CFDI40102");
    }

    [Fact]
    public async Task Un_certificado_que_no_es_un_cer_valido_es_CFDI40106_y_no_hay_sello_que_comprobar()
    {
        var xml = Basico();
        var certificado = Texto(xml, "Certificado");

        var reporte = await Validar(xml.Replace(certificado, "AAAA", StringComparison.Ordinal));

        Assert.False(reporte.EsValido);
        Assert.Null(reporte.SelloValido);
        Assert.Null(reporte.Certificado);
        var hallazgo = Assert.Single(reporte.Hallazgos);
        Assert.Equal("CFDI40106", hallazgo.Codigo);
        Assert.Equal("Certificado", hallazgo.Atributo);
        Assert.True(hallazgo.Linea > 0);
    }

    [Fact]
    public async Task Un_comprobante_sin_sello_ni_certificado_solo_tiene_los_hallazgos_del_esquema()
    {
        var xml = Basico();
        var sinSello = xml.Replace($" Sello=\"{Texto(xml, "Sello")}\"", string.Empty, StringComparison.Ordinal)
            .Replace($" Certificado=\"{Texto(xml, "Certificado")}\"", string.Empty, StringComparison.Ordinal);

        var reporte = await Validar(sinSello);

        Assert.Null(reporte.SelloValido);
        Assert.Null(reporte.Certificado);
        Assert.Contains(reporte.Hallazgos, h => h.Codigo == "CMP003" && h.Atributo == "Sello"
            && h.Mensaje == "Falta el atributo requerido Sello.");
        Assert.Contains(reporte.Hallazgos, h => h.Codigo == "CMP003" && h.Atributo == "Certificado");
    }

    [Fact]
    public async Task Falta_un_atributo_requerido()
    {
        var reporte = await Validar(Basico().Replace(" Total=\"1160.00\"", string.Empty, StringComparison.Ordinal));

        var hallazgo = reporte.Hallazgos.Single(h => h.Codigo == "CMP003");
        Assert.Equal("Total", hallazgo.Atributo);
        Assert.Equal("Falta el atributo requerido Total.", hallazgo.Mensaje);
        Assert.Equal("cfdi:Comprobante", hallazgo.Nodo);
        Assert.Equal(Severidad.Error, hallazgo.Severidad);
        Assert.True(hallazgo.Linea > 0);
    }

    [Fact]
    public void El_esquema_dice_que_un_valor_fuera_de_un_catalogo_no_es_uno_de_los_permitidos()
    {
        var documento = XDocument.Parse(Basico().Replace("Moneda=\"MXN\"", "Moneda=\"ZZZ\"", StringComparison.Ordinal));

        var hallazgo = ValidadorXsd.Validar(documento).Single();

        Assert.Equal("CMP003", hallazgo.Codigo);
        Assert.Equal("Moneda", hallazgo.Atributo);
        Assert.Equal("cfdi:Comprobante", hallazgo.Nodo);
        Assert.Contains("\"ZZZ\"", hallazgo.Mensaje, StringComparison.Ordinal);
        Assert.Contains("no es uno de los valores permitidos", hallazgo.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void El_esquema_dice_que_un_valor_con_otro_formato_no_tiene_el_formato_que_pide()
    {
        var documento = XDocument.Parse(
            Basico().Replace("Fecha=\"2026-11-21T09:30:00\"", "Fecha=\"2026-11-21 09:30:00\"", StringComparison.Ordinal));

        var hallazgo = ValidadorXsd.Validar(documento).Single();

        Assert.Equal("CMP003", hallazgo.Codigo);
        Assert.Equal("Fecha", hallazgo.Atributo);
        Assert.Contains("no tiene el formato que pide el esquema", hallazgo.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cuando_una_regla_del_SAT_senala_el_mismo_atributo_que_el_esquema_queda_el_hallazgo_del_SAT()
    {
        var moneda = await Validar(Basico().Replace("Moneda=\"MXN\"", "Moneda=\"ZZZ\"", StringComparison.Ordinal));
        var fecha = await Validar(
            Basico().Replace("Fecha=\"2026-11-21T09:30:00\"", "Fecha=\"2026-11-21 09:30:00\"", StringComparison.Ordinal));

        var porMoneda = Assert.Single(moneda.Hallazgos, h => h.Atributo == "Moneda");
        Assert.Equal("CFDI40113", porMoneda.Codigo);
        Assert.Contains("ZZZ", porMoneda.Mensaje, StringComparison.Ordinal);
        var porFecha = Assert.Single(fecha.Hallazgos, h => h.Atributo == "Fecha");
        Assert.Equal("CFDI40101", porFecha.Codigo);
        Assert.DoesNotContain(moneda.Hallazgos.Concat(fecha.Hallazgos), h => h.Codigo == "CMP003");
    }

    [Fact]
    public async Task Un_atributo_que_el_esquema_no_conoce()
    {
        var reporte = await Validar(Basico().Replace("Version=\"4.0\"", "Version=\"4.0\" Descuentito=\"1\"", StringComparison.Ordinal));

        var hallazgo = reporte.Hallazgos.Single(h => h.Codigo == "CMP003");
        Assert.Equal("Descuentito", hallazgo.Atributo);
        Assert.Equal("El atributo Descuentito no existe en el esquema del CFDI 4.0.", hallazgo.Mensaje);
    }

    [Fact]
    public async Task Un_nodo_fuera_de_orden_o_de_mas_se_senala_en_el_nodo_que_sobra()
    {
        var xml = Basico().Replace(
            "  <cfdi:Conceptos>",
            "  <cfdi:Extra Dato=\"1\" />\n  <cfdi:Conceptos>",
            StringComparison.Ordinal);

        var reporte = await Validar(xml);

        // El hallazgo apunta al nodo que sobra, y el mensaje dice dentro de cuál va y qué se esperaba.
        var hallazgo = reporte.Hallazgos.First(h => h.Codigo == "CMP003");
        Assert.Equal("cfdi:Comprobante/cfdi:Extra", hallazgo.Nodo);
        Assert.StartsWith("El elemento Extra no puede ir en ese lugar dentro de Comprobante", hallazgo.Mensaje, StringComparison.Ordinal);
        Assert.Contains("Se esperaba: Conceptos", hallazgo.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Los_nodos_repetidos_se_distinguen_con_su_posicion()
    {
        // Segundo concepto sin ClaveUnidad: el hallazgo debe decir cuál.
        var xml = Referencia.Xml("ingreso-retenciones").Replace(
            "ClaveUnidad=\"E48\" Unidad=\"Unidad de servicio\" Descripcion=\"Configuración de software\"",
            "Unidad=\"Unidad de servicio\" Descripcion=\"Configuración de software\"",
            StringComparison.Ordinal);

        var reporte = await Validar(xml);

        var hallazgo = reporte.Hallazgos.Single(h => h.Codigo == "CMP003");
        Assert.Equal("ClaveUnidad", hallazgo.Atributo);
        Assert.Equal("cfdi:Comprobante/cfdi:Conceptos/cfdi:Concepto[2]", hallazgo.Nodo);
    }

    [Fact]
    public async Task Un_nodo_incompleto_dice_que_falta()
    {
        const string xml =
            """
            <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" Version="4.0" Fecha="2026-11-21T09:30:00" Sello="AA" NoCertificado="30001000000500003416" Certificado="AA" SubTotal="1.00" Moneda="MXN" Total="1.00" TipoDeComprobante="I" Exportacion="01" LugarExpedicion="42501">
              <cfdi:Emisor Rfc="EKU9003173C9" Nombre="ESCUELA KEMPER URGATE" RegimenFiscal="601" />
            </cfdi:Comprobante>
            """;

        var reporte = await Validar(xml);

        var hallazgo = reporte.Hallazgos.First(h => h.Mensaje.Contains("incompleto", StringComparison.Ordinal));
        Assert.Equal("CMP003", hallazgo.Codigo);
        Assert.Equal("cfdi:Comprobante", hallazgo.Nodo);
        Assert.Equal("El elemento Comprobante está incompleto. Falta: Receptor.", hallazgo.Mensaje);
    }

    [Fact]
    public async Task Se_puede_validar_desde_varios_hilos_a_la_vez()
    {
        var xml = Basico();

        var reportes = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => Validar(xml))));

        Assert.All(reportes, reporte =>
        {
            Assert.True(reporte.EsValido, Resumen(reporte));
            Assert.True(reporte.SelloValido);
        });
    }

    [Fact]
    public void Los_hallazgos_se_serializan_con_la_severidad_en_texto_y_sin_los_campos_vacios()
    {
        var hallazgos = new[]
        {
            new Hallazgo("CFDI40102", Severidad.Error, "cfdi:Comprobante", "Sello", "El sello no corresponde."),
            new Hallazgo("CMP101", Severidad.Advertencia, "cfdi:Comprobante", "Fecha", "Está en el futuro.", Encontrado: "2030-01-01T00:00:00"),
        };

        var json = System.Text.Json.JsonSerializer.Serialize(hallazgos, Referencia.OpcionesJson);

        Assert.Contains("\"severidad\":\"error\"", json, StringComparison.Ordinal);
        Assert.Contains("\"severidad\":\"advertencia\"", json, StringComparison.Ordinal);
        Assert.Contains("\"encontrado\":\"2030-01-01T00:00:00\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("esto no es xml")]
    [InlineData("<cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/4\">")]
    [InlineData("<a><b></a></b>")]
    public async Task Un_texto_que_no_es_XML_bien_formado_es_CMP001_y_no_se_sigue(string xml)
    {
        var reporte = await Validar(xml);

        Assert.False(reporte.EsValido);
        var hallazgo = Assert.Single(reporte.Hallazgos);
        Assert.Equal("CMP001", hallazgo.Codigo);
        Assert.Null(reporte.CadenaOriginal);
        Assert.Null(reporte.SelloValido);
    }

    [Fact]
    public async Task Otro_nodo_raiz_no_es_un_CFDI_40()
    {
        var reporte = await Validar("<factura><total>1</total></factura>");

        var hallazgo = Assert.Single(reporte.Hallazgos);
        Assert.Equal("CMP002", hallazgo.Codigo);
        Assert.Equal("factura", hallazgo.Nodo);
    }

    [Fact]
    public async Task Un_CFDI_33_se_rechaza_por_su_espacio_de_nombres_y_su_version()
    {
        var reporte = await Validar(
            "<cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/3\" Version=\"3.3\" Total=\"1.00\" />");

        var hallazgo = Assert.Single(reporte.Hallazgos);
        Assert.Equal("CMP002", hallazgo.Codigo);

        var version = await Validar(Basico().Replace("Version=\"4.0\"", "Version=\"3.3\"", StringComparison.Ordinal));
        var hallazgoVersion = Assert.Single(version.Hallazgos);
        Assert.Equal("CMP002", hallazgoVersion.Codigo);
        Assert.Equal("Version", hallazgoVersion.Atributo);
        Assert.Equal("3.3", hallazgoVersion.Encontrado);
        Assert.Equal("4.0", hallazgoVersion.Esperado);
    }

    [Fact]
    public async Task Un_DTD_con_una_entidad_externa_no_se_procesa_ni_se_filtra_su_contenido()
    {
        const string xxe =
            """
            <?xml version="1.0"?>
            <!DOCTYPE cfdi [ <!ENTITY secreto SYSTEM "file:///etc/passwd"> ]>
            <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" Version="4.0" Total="&secreto;" />
            """;

        var reporte = await Validar(xxe);

        var hallazgo = Assert.Single(reporte.Hallazgos);
        Assert.Equal("CMP001", hallazgo.Codigo);
        Assert.DoesNotContain("root:", hallazgo.Mensaje, StringComparison.Ordinal);
        Assert.False(reporte.EsValido);
    }

    [Fact]
    public async Task Una_bomba_de_entidades_billion_laughs_se_corta_sin_expandirse()
    {
        var bomba = new StringBuilder("<?xml version=\"1.0\"?>\n<!DOCTYPE lolz [\n<!ENTITY lol \"lol\">\n");
        for (var nivel = 1; nivel <= 9; nivel++)
        {
            var anterior = nivel == 1 ? "lol" : $"lol{nivel - 1}";
            bomba.Append($"<!ENTITY lol{nivel} \"{string.Concat(Enumerable.Repeat($"&{anterior};", 10))}\">\n");
        }

        bomba.Append("]>\n<lolz>&lol9;</lolz>");

        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        var reporte = await Validar(bomba.ToString());
        cronometro.Stop();

        Assert.Equal("CMP001", Assert.Single(reporte.Hallazgos).Codigo);
        Assert.True(cronometro.Elapsed < TimeSpan.FromSeconds(2), $"Tardó {cronometro.Elapsed}");
    }

    [Fact]
    public async Task Un_XML_de_mas_de_dos_megas_se_rechaza()
    {
        var xml = Basico().Replace("</cfdi:Comprobante>", $"<!-- {new string('x', 2 * 1024 * 1024)} --></cfdi:Comprobante>", StringComparison.Ordinal);

        var reporte = await Validar(xml);

        Assert.Equal("CMP001", Assert.Single(reporte.Hallazgos).Codigo);
    }

    [Fact]
    public async Task Los_errores_de_esquema_de_un_XML_destrozado_se_limitan()
    {
        var conceptos = string.Concat(Enumerable.Repeat("<cfdi:Concepto Mal=\"1\" />", 400));
        var xml = Basico().Replace("<cfdi:Conceptos>", $"<cfdi:Conceptos>{conceptos}", StringComparison.Ordinal);

        var reporte = await Validar(xml);

        Assert.True(reporte.Hallazgos.Count <= ValidadorXsd.MaximoDeHallazgos + 2);
        Assert.Contains(reporte.Hallazgos, h => h.Mensaje.Contains("errores de esquema más", StringComparison.Ordinal));
    }

    /// <summary>El valor de un atributo del nodo raíz (los de la referencia no repiten nombres con otro nodo antes).</summary>
    private static string Texto(string xml, string atributo)
    {
        var inicio = xml.IndexOf($" {atributo}=\"", StringComparison.Ordinal) + atributo.Length + 3;
        return xml[inicio..xml.IndexOf('"', inicio)];
    }
}
