using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Comprobante.IntegrationTests;

[Collection(ColeccionApi.Nombre)]
public partial class GeneracionApiTests(ApiFactory api)
{
    private const string Ruta = "/api/comprobantes";
    private static readonly XNamespace Cfdi = "http://www.sat.gob.mx/cfd/4";

    private readonly HttpClient _http = api.CreateClient();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$")]
    private static partial Regex FormatoDeFecha();

    private static object Ingreso(object? fecha = null) => new
    {
        serie = "A",
        folio = "123",
        fecha,
        formaPago = "01",
        metodoPago = "PUE",
        emisor = new { rfc = "EKU9003173C9" },
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

    [Fact]
    public async Task Lista_los_diez_emisores_de_prueba_sin_llaves_ni_contrasenas()
    {
        var respuesta = await _http.GetAsync("/api/emisores-prueba");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        var emisores = JsonDocument.Parse(texto).RootElement;
        Assert.Equal(10, emisores.GetArrayLength());

        var eku = emisores.EnumerateArray().Single(e => e.GetProperty("rfc").GetString() == "EKU9003173C9");
        Assert.Equal("ESCUELA KEMPER URGATE", eku.GetProperty("nombre").GetString());
        Assert.Equal("moral", eku.GetProperty("persona").GetString());
        Assert.Equal("42501", eku.GetProperty("codigoPostal").GetString());
        Assert.Equal("601", eku.GetProperty("regimenFiscal").GetString());
        Assert.Equal("30001000000500003416", eku.GetProperty("noCertificado").GetString());
        Assert.True(eku.GetProperty("validoHasta").GetDateTimeOffset() > eku.GetProperty("validoDesde").GetDateTimeOffset());

        // Solo lo que se muestra: nada de la llave privada ni de su contraseña.
        Assert.DoesNotContain("12345678a", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("key", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contrasena", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(emisores.EnumerateArray(), e => e.GetProperty("rfc").GetString() == "H&E951128469");
        Assert.Contains(emisores.EnumerateArray(), e => e.GetProperty("rfc").GetString() == "IAÑL750210963");
    }

    [Fact]
    public async Task Genera_un_ingreso_sellado_que_pasa_el_esquema_y_cuyo_sello_verifica()
    {
        var respuesta = await _http.PostAsJsonAsync(Ruta, Ingreso(fecha: "2026-11-21T09:30:00"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/json", respuesta.Content.Headers.ContentType?.MediaType);
        var resultado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("EKU9003173C9-A-123-prueba.xml", resultado.GetProperty("nombreArchivo").GetString());
        Assert.Equal("30001000000500003416", resultado.GetProperty("noCertificado").GetString());
        Assert.StartsWith("||4.0|A|123|2026-11-21T09:30:00|01|30001000000500003416|", resultado.GetProperty("cadenaOriginal").GetString());
        Assert.Equal(1160.00m, resultado.GetProperty("calculo").GetProperty("total").GetDecimal());

        var raiz = XDocument.Parse(resultado.GetProperty("xml").GetString()!).Root!;
        Assert.Equal(Cfdi + "Comprobante", raiz.Name);
        Assert.Equal("1160.00", raiz.Attribute("Total")!.Value);
        Assert.Equal(resultado.GetProperty("sello").GetString(), raiz.Attribute("Sello")!.Value);

        var validacion = resultado.GetProperty("validacion");
        Assert.True(validacion.GetProperty("esValido").GetBoolean());
        Assert.True(validacion.GetProperty("selloValido").GetBoolean());
        Assert.Equal(0, validacion.GetProperty("hallazgos").GetArrayLength());
        Assert.Equal("EKU9003173C9", validacion.GetProperty("certificado").GetProperty("rfc").GetString());
    }

    [Fact]
    public async Task Sin_fecha_el_servidor_pone_la_hora_local_del_lugar_de_expedicion()
    {
        var respuesta = await _http.PostAsJsonAsync(Ruta, Ingreso());

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var fecha = XDocument.Parse(resultado.GetProperty("xml").GetString()!).Root!.Attribute("Fecha")!.Value;
        Assert.Matches(FormatoDeFecha(), fecha);

        // Es la hora de México, no la del servidor (UTC): entre 5 y 7 horas antes (centro, sin horario de verano) o cerca de ellas.
        var mexico = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"));
        var generada = DateTime.ParseExact(fecha, "yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange((generada - mexico.DateTime).TotalMinutes, -5, 5);
    }

    [Fact]
    public async Task Un_emisor_que_no_es_de_prueba_vuelve_como_error_de_su_campo()
    {
        var comprobante = JsonSerializer.SerializeToNode(Ingreso())!;
        comprobante["emisor"]!["rfc"] = "XAXX010101000";

        var respuesta = await _http.PostAsJsonAsync(Ruta, comprobante);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Hay datos que corregir", problema.GetProperty("title").GetString());
        Assert.Contains("certificado de prueba", problema.GetProperty("errors").GetProperty("emisor.rfc")[0].GetString());
    }

    [Fact]
    public async Task Los_errores_del_emisor_y_de_los_conceptos_vuelven_juntos()
    {
        var respuesta = await _http.PostAsJsonAsync(Ruta, new { emisor = new { rfc = "AAA010101AAA" }, conceptos = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var errores = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errores.TryGetProperty("emisor.rfc", out _));
        Assert.True(errores.TryGetProperty("conceptos", out _));
    }

    [Fact]
    public async Task Sin_cuerpo_pide_el_comprobante()
    {
        var respuesta = await _http.PostAsync(Ruta, new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Hay datos que corregir", problema.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Otro_tipo_de_contenido_da_415()
    {
        var respuesta = await _http.PostAsync(Ruta, new StringContent("<a/>", Encoding.UTF8, "application/xml"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_documento_OpenAPI_describe_la_generacion_y_los_emisores()
    {
        var documento = JsonDocument.Parse(await _http.GetStringAsync("/api/openapi/v1.json")).RootElement;

        var rutas = documento.GetProperty("paths");
        Assert.True(rutas.GetProperty("/api/comprobantes").TryGetProperty("post", out var generar));
        Assert.StartsWith("Arma, sella y revisa", generar.GetProperty("summary").GetString());
        Assert.True(rutas.GetProperty("/api/emisores-prueba").TryGetProperty("get", out _));
    }
}
