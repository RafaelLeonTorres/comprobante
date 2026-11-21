using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Comprobante.IntegrationTests;

[Collection(ColeccionApi.Nombre)]
public class ValidacionesApiTests(ApiFactory api)
{
    private const string Ruta = "/api/validaciones";
    private const int DosMegabytes = 2 * 1024 * 1024;

    private readonly HttpClient _http = api.CreateClient();

    private static object Ingreso(string rfcEmisor) => new
    {
        serie = "A",
        folio = "1",
        fecha = "2026-11-21T09:30:00",
        formaPago = "01",
        metodoPago = "PUE",
        emisor = new { rfc = rfcEmisor },
        receptor = new
        {
            rfc = "XIA190128J61",
            nombre = "XENON INDUSTRIAL ARTICLES",
            domicilioFiscalReceptor = "76343",
            regimenFiscalReceptor = "622",
            usoCfdi = "G03",
        },
        conceptos = new[]
        {
            new
            {
                claveProdServ = "84111506",
                claveUnidad = "E48",
                unidad = "Unidad de servicio",
                descripcion = "Servicio de facturación electrónica",
                cantidad = 1,
                valorUnitario = 1000.00m,
                traslados = new[] { new { impuesto = "002", tipoFactor = "Tasa", tasaOCuota = 0.16m } },
            },
        },
    };

    /// <summary>Un CFDI sellado, tal como lo genera la propia app.</summary>
    private async Task<string> GenerarXmlAsync(string rfcEmisor = "EKU9003173C9")
    {
        var respuesta = await _http.PostAsJsonAsync("/api/comprobantes", Ingreso(rfcEmisor));
        respuesta.EnsureSuccessStatusCode();
        var resultado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return resultado.GetProperty("xml").GetString()!;
    }

    private static StringContent Xml(string xml, string tipo = "application/xml") =>
        new(xml, new UTF8Encoding(false), tipo);

    private static async Task<JsonElement> LeerAsync(HttpResponseMessage respuesta) =>
        await respuesta.Content.ReadFromJsonAsync<JsonElement>();

    private static string[] Codigos(JsonElement reporte) =>
        reporte.GetProperty("hallazgos").EnumerateArray().Select(h => h.GetProperty("codigo").GetString()!).ToArray();

    [Fact]
    public async Task Un_cfdi_generado_por_la_app_se_valida_sin_hallazgos()
    {
        var respuesta = await _http.PostAsync(Ruta, Xml(await GenerarXmlAsync()));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/json", respuesta.Content.Headers.ContentType?.MediaType);
        var reporte = await LeerAsync(respuesta);
        Assert.True(reporte.GetProperty("esValido").GetBoolean());
        Assert.Equal(0, reporte.GetProperty("hallazgos").GetArrayLength());
        Assert.True(reporte.GetProperty("selloValido").GetBoolean());
        Assert.StartsWith("||4.0|A|1|2026-11-21T09:30:00|", reporte.GetProperty("cadenaOriginal").GetString());
        Assert.Equal("EKU9003173C9", reporte.GetProperty("certificado").GetProperty("rfc").GetString());
    }

    [Fact]
    public async Task Acepta_text_xml_igual_que_application_xml()
    {
        var respuesta = await _http.PostAsync(Ruta, Xml(await GenerarXmlAsync(), "text/xml"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True((await LeerAsync(respuesta)).GetProperty("esValido").GetBoolean());
    }

    [Fact]
    public async Task Un_total_alterado_se_detecta_por_la_regla_del_total_y_por_el_sello()
    {
        var xml = (await GenerarXmlAsync()).Replace("Total=\"1160.00\"", "Total=\"1161.00\"", StringComparison.Ordinal);

        var respuesta = await _http.PostAsync(Ruta, Xml(xml));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode); // los hallazgos no son un error de la solicitud
        var reporte = await LeerAsync(respuesta);
        Assert.False(reporte.GetProperty("esValido").GetBoolean());
        Assert.False(reporte.GetProperty("selloValido").GetBoolean());
        Assert.Equal(["CFDI40119", "CFDI40102"], Codigos(reporte));
        var total = reporte.GetProperty("hallazgos")[0];
        Assert.Equal("error", total.GetProperty("severidad").GetString());
        Assert.Equal("cfdi:Comprobante", total.GetProperty("nodo").GetString());
        Assert.Equal("Total", total.GetProperty("atributo").GetString());
        Assert.Equal("1161.00", total.GetProperty("encontrado").GetString());
        Assert.Equal("1160.00", total.GetProperty("esperado").GetString());
        Assert.True(total.GetProperty("linea").GetInt32() > 0);
    }

    [Fact]
    public async Task Una_clave_fuera_del_catalogo_sale_con_el_codigo_del_SAT_y_el_lugar_exacto()
    {
        var xml = (await GenerarXmlAsync()).Replace("UsoCFDI=\"G03\"", "UsoCFDI=\"Z99\"", StringComparison.Ordinal);

        var reporte = await LeerAsync(await _http.PostAsync(Ruta, Xml(xml)));

        var uso = reporte.GetProperty("hallazgos").EnumerateArray().Single(h => h.GetProperty("codigo").GetString() == "CFDI40160");
        Assert.Equal("cfdi:Comprobante/cfdi:Receptor", uso.GetProperty("nodo").GetString());
        Assert.Equal("UsoCFDI", uso.GetProperty("atributo").GetString());
        Assert.Equal("Z99", uso.GetProperty("encontrado").GetString());
    }

    [Fact]
    public async Task Respeta_el_charset_que_declara_la_solicitud()
    {
        // El emisor IAÑL750210963 lleva Ñ en el RFC y en el nombre: si se lee con otra codificación, el sello no verifica.
        var xml = await GenerarXmlAsync("IAÑL750210963");
        var contenido = new ByteArrayContent(Encoding.Latin1.GetBytes(xml));
        contenido.Headers.ContentType = MediaTypeHeaderValue.Parse("application/xml; charset=iso-8859-1");

        var reporte = await LeerAsync(await _http.PostAsync(Ruta, contenido));

        Assert.True(reporte.GetProperty("esValido").GetBoolean(), reporte.ToString());
        Assert.True(reporte.GetProperty("selloValido").GetBoolean());
        Assert.Equal("IAÑL750210963", reporte.GetProperty("certificado").GetProperty("rfc").GetString());
    }

    [Fact]
    public async Task Un_xml_en_utf8_con_BOM_se_lee_igual()
    {
        var xml = await GenerarXmlAsync();
        var contenido = new ByteArrayContent([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(xml)]);
        contenido.Headers.ContentType = MediaTypeHeaderValue.Parse("application/xml");

        var reporte = await LeerAsync(await _http.PostAsync(Ruta, contenido));

        Assert.True(reporte.GetProperty("esValido").GetBoolean(), reporte.ToString());
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    public async Task Otro_tipo_de_contenido_da_415(string tipo)
    {
        var respuesta = await _http.PostAsync(Ruta, new StringContent("<a/>", Encoding.UTF8, tipo));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await LeerAsync(respuesta);
        Assert.Equal("Tipo de contenido no admitido", problema.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Sin_tipo_de_contenido_da_415()
    {
        var respuesta = await _http.PostAsync(Ruta, new ByteArrayContent(Encoding.UTF8.GetBytes("<a/>")));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n ")]
    public async Task Un_cuerpo_vacio_da_400(string cuerpo)
    {
        var respuesta = await _http.PostAsync(Ruta, Xml(cuerpo));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await LeerAsync(respuesta);
        Assert.Contains("vacío", problema.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_xml_de_mas_de_2_MB_da_413_sin_leerse()
    {
        var enorme = "<a>" + new string('x', DosMegabytes) + "</a>";

        var respuesta = await _http.PostAsync(Ruta, Xml(enorme));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task El_limite_de_2_MB_tambien_vale_cuando_el_cuerpo_llega_sin_longitud()
    {
        var enorme = Encoding.UTF8.GetBytes("<a>" + new string('x', DosMegabytes) + "</a>");

        var respuesta = await _http.PostAsync(Ruta, new ContenidoSinLongitud(enorme, "application/xml"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_DOCTYPE_no_se_procesa_y_se_explica()
    {
        const string ataque = "<?xml version=\"1.0\"?><!DOCTYPE a [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><a>&x;</a>";

        var respuesta = await _http.PostAsync(Ruta, Xml(ataque));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var reporte = await LeerAsync(respuesta);
        Assert.False(reporte.GetProperty("esValido").GetBoolean());
        var hallazgo = Assert.Single(reporte.GetProperty("hallazgos").EnumerateArray());
        Assert.Equal("CMP001", hallazgo.GetProperty("codigo").GetString());
        Assert.Contains("DOCTYPE", hallazgo.GetProperty("mensaje").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("root:", await respuesta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_xml_mal_formado_vuelve_como_hallazgo_con_su_linea()
    {
        var respuesta = await _http.PostAsync(Ruta, Xml("<a>\n  <b>\n  </c>\n</a>"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var hallazgo = Assert.Single((await LeerAsync(respuesta)).GetProperty("hallazgos").EnumerateArray());
        Assert.Equal("CMP001", hallazgo.GetProperty("codigo").GetString());
        Assert.Equal(3, hallazgo.GetProperty("linea").GetInt32());
    }

    [Fact]
    public async Task La_lista_de_reglas_trae_las_129_de_la_matriz_con_lo_que_la_app_hace_con_cada_una()
    {
        var respuesta = await _http.GetAsync($"{Ruta}/reglas");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cobertura = await LeerAsync(respuesta);
        var resumen = cobertura.GetProperty("resumen");
        Assert.Equal(129, resumen.GetProperty("total").GetInt32());
        Assert.Equal(103, resumen.GetProperty("revisadas").GetInt32());
        Assert.Equal(5, resumen.GetProperty("parciales").GetInt32());
        Assert.Equal(21, resumen.GetProperty("noRevisadas").GetInt32());

        var reglas = cobertura.GetProperty("reglas").EnumerateArray().ToList();
        Assert.Equal(129, reglas.Count);
        Assert.Equal(129, reglas.Select(r => r.GetProperty("codigo").GetString()).Distinct().Count());
        Assert.Equal("CFDI40101", reglas[0].GetProperty("codigo").GetString());
        Assert.All(reglas, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.GetProperty("elemento").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(r.GetProperty("descripcion").GetString()));
            Assert.DoesNotContain("  ", r.GetProperty("descripcion").GetString(), StringComparison.Ordinal);
            Assert.Contains(r.GetProperty("estado").GetString(), new[] { "revisada", "parcial", "noRevisada" });
        });

        var sinRevisar = reglas.Single(r => r.GetProperty("codigo").GetString() == "CFDI40138");
        Assert.Equal("noRevisada", sinRevisar.GetProperty("estado").GetString());
        Assert.Contains("LRFC", sinRevisar.GetProperty("nota").GetString(), StringComparison.Ordinal);

        var cubierta = reglas.Single(r => r.GetProperty("codigo").GetString() == "CFDI40209");
        Assert.Equal("CFDI40206", cubierta.GetProperty("cubiertaPor").GetString());

        // Una regla de un nodo completo no trae atributo.
        var delNodo = reglas.Single(r => r.GetProperty("codigo").GetString() == "CFDI40130");
        Assert.False(delNodo.TryGetProperty("atributo", out _));
        Assert.Equal("cfdi:InformacionGlobal", delNodo.GetProperty("elemento").GetString());
    }

    [Fact]
    public async Task El_documento_OpenAPI_describe_la_validacion_y_la_lista_de_reglas()
    {
        var documento = JsonDocument.Parse(await _http.GetStringAsync("/api/openapi/v1.json")).RootElement;

        var rutas = documento.GetProperty("paths");
        Assert.True(rutas.GetProperty(Ruta).TryGetProperty("post", out var validar));
        Assert.StartsWith("Valida un XML de CFDI 4.0", validar.GetProperty("summary").GetString(), StringComparison.Ordinal);
        var respuestas = validar.GetProperty("responses");
        Assert.True(respuestas.TryGetProperty("200", out _));
        Assert.True(respuestas.TryGetProperty("413", out _));
        Assert.True(respuestas.TryGetProperty("415", out _));
        Assert.True(rutas.GetProperty($"{Ruta}/reglas").TryGetProperty("get", out _));
    }

    /// <summary>Un cuerpo que se envía "por partes": sin Content-Length, para probar el límite que se cuenta mientras se lee.</summary>
    private sealed class ContenidoSinLongitud : HttpContent
    {
        private readonly byte[] _bytes;

        public ContenidoSinLongitud(byte[] bytes, string tipo)
        {
            _bytes = bytes;
            Headers.ContentType = MediaTypeHeaderValue.Parse(tipo);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_bytes, 0, _bytes.Length);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
