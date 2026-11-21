using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Comprobante.IntegrationTests;

[Collection(ColeccionApi.Nombre)]
public class CalculoApiTests(ApiFactory api)
{
    private const string Ruta = "/api/comprobantes/calculo";

    private readonly HttpClient _http = api.CreateClient();

    private static object ConceptoConIva(object cantidad, object valorUnitario) => new
    {
        claveProdServ = "84111506",
        claveUnidad = "E48",
        descripcion = "Servicio de prueba",
        cantidad,
        valorUnitario,
        traslados = new[] { new { impuesto = "002", tipoFactor = "Tasa", tasaOCuota = 0.16m } },
    };

    [Fact]
    public async Task Calcula_importes_impuestos_y_total()
    {
        var respuesta = await _http.PostAsJsonAsync(Ruta, new { conceptos = new[] { ConceptoConIva(1, 100.00m) } });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var calculo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MXN", calculo.GetProperty("moneda").GetString());
        Assert.Equal(2, calculo.GetProperty("decimalesMoneda").GetInt32());
        Assert.Equal(100.00m, calculo.GetProperty("subTotal").GetDecimal());
        Assert.Equal(116.00m, calculo.GetProperty("total").GetDecimal());
        Assert.Equal(16.00m, calculo.GetProperty("totalImpuestosTrasladados").GetDecimal());
        var traslado = calculo.GetProperty("traslados")[0];
        Assert.Equal("002", traslado.GetProperty("impuesto").GetString());
        Assert.Equal("Tasa", traslado.GetProperty("tipoFactor").GetString());
        Assert.Equal(0.16m, traslado.GetProperty("tasaOCuota").GetDecimal());
        Assert.Equal(16.00m, traslado.GetProperty("importe").GetDecimal());
        // Lo que no aplica no se manda: sin descuento ni retenciones.
        Assert.False(calculo.TryGetProperty("descuento", out _));
        Assert.False(calculo.TryGetProperty("totalImpuestosRetenidos", out _));
    }

    [Fact]
    public async Task Acepta_los_numeros_como_texto_y_las_letras_en_cualquier_caso()
    {
        // Un formulario puede mandar "100.00" y no 100.00; "TASA" y "tasa" valen lo mismo.
        var concepto = new
        {
            claveProdServ = "84111506",
            claveUnidad = "e48",
            descripcion = "Servicio",
            cantidad = "2",
            valorUnitario = "50.00",
            traslados = new[] { new { impuesto = "002", tipoFactor = "TASA", tasaOCuota = "0.16" } },
        };

        var respuesta = await _http.PostAsJsonAsync(Ruta, new { moneda = "mxn", conceptos = new[] { concepto } });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var calculo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(116.00m, calculo.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Los_errores_vuelven_por_campo_en_espanol()
    {
        var respuesta = await _http.PostAsJsonAsync(Ruta, new
        {
            moneda = "ZZZ",
            conceptos = new object[]
            {
                ConceptoConIva(1, 10m),
                ConceptoConIva(0, 10m),
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Hay datos que corregir", problema.GetProperty("title").GetString());
        var errores = problema.GetProperty("errors");
        Assert.Contains("c_Moneda", errores.GetProperty("moneda")[0].GetString());
        Assert.Contains("mayor que cero", errores.GetProperty("conceptos[1].cantidad")[0].GetString());
        Assert.False(errores.TryGetProperty("conceptos[0].cantidad", out _));
    }

    [Fact]
    public async Task Sin_cuerpo_pide_el_comprobante()
    {
        var respuesta = await _http.PostAsync(Ruta, new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Hay datos que corregir", problema.GetProperty("title").GetString());
    }

    private async Task<JsonElement> PostearJsonAsync(string cuerpo)
    {
        var respuesta = await _http.PostAsync(Ruta, new StringContent(cuerpo, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("La solicitud no es válida", problema.GetProperty("title").GetString());
        Assert.Contains("formato", problema.GetProperty("detail").GetString());
        return problema.GetProperty("errors");
    }

    [Fact]
    public async Task Un_json_con_errores_de_sintaxis_dice_donde_esta_el_error()
    {
        var errores = await PostearJsonAsync("{ esto no es json");

        var mensaje = errores.GetProperty("")[0].GetString();
        Assert.Equal("El cuerpo no es un JSON válido (línea 1, posición 3): revisa comas, comillas y llaves.", mensaje);
    }

    [Fact]
    public async Task Un_valor_que_no_es_del_tipo_esperado_se_senala_en_su_campo()
    {
        var errores = await PostearJsonAsync("""{ "conceptos": [ { "cantidad": "abc" } ] }""");

        // Sin "System.Decimal" ni posiciones de bytes: el mensaje es para quien llena el formulario.
        Assert.Equal("El valor debe ser un número.", errores.GetProperty("conceptos[0].cantidad")[0].GetString());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("5")]
    [InlineData("\"texto\"")]
    public async Task Un_cuerpo_que_no_es_un_objeto_json_pide_el_objeto(string cuerpo)
    {
        var errores = await PostearJsonAsync(cuerpo);

        Assert.Equal(
            "El cuerpo debe ser un objeto JSON con los datos del comprobante.",
            errores.GetProperty("")[0].GetString());
    }

    [Fact]
    public async Task Otro_tipo_de_contenido_da_415_en_espanol()
    {
        var respuesta = await _http.PostAsync(Ruta, new StringContent("<a/>", Encoding.UTF8, "application/xml"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Tipo de contenido no admitido", problema.GetProperty("title").GetString());
    }
}
