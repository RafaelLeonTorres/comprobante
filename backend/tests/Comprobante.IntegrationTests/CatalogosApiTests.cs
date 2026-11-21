using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Comprobante.Catalogos;
using Microsoft.Extensions.DependencyInjection;

namespace Comprobante.IntegrationTests;

[Collection(ColeccionApi.Nombre)]
public class CatalogosApiTests(ApiFactory api)
{
    private readonly HttpClient _http = api.CreateClient();

    [Fact]
    public async Task La_api_responde_ok_cuando_ya_cargo_los_catalogos()
    {
        var respuesta = await _http.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var json = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", json.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Lista_los_diecinueve_catalogos_con_la_version_cargada()
    {
        var json = await _http.GetFromJsonAsync<JsonElement>("/api/catalogos");

        var catalogos = json.GetProperty("catalogos").EnumerateArray()
            .ToDictionary(c => c.GetProperty("nombre").GetString()!, c => c.GetProperty("filas").GetInt32());
        Assert.Equal(19, catalogos.Count);
        Assert.All(catalogos, c => Assert.True(c.Value > 0, $"El catálogo {c.Key} está vacío"));
        Assert.True(catalogos["clave-prod-serv"] > 50_000);
        Assert.True(catalogos["codigo-postal"] > 90_000);
        Assert.Equal(3, catalogos["impuesto"]);

        var version = json.GetProperty("version");
        Assert.StartsWith("catCFDI_V_4_", version.GetProperty("archivo").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", version.GetProperty("publicacion").GetString()!);
    }

    [Theory]
    [InlineData("clave-prod-serv", "gatos", "10101501")]
    [InlineData("clave-unidad", "pieza", "H87")]
    [InlineData("codigo-postal", "42501", "42501")]
    [InlineData("moneda", "mxn", "MXN")]
    [InlineData("uso-cfdi", "S01", "S01")]
    [InlineData("regimen-fiscal", "sueldos", "605")]
    public async Task La_busqueda_encuentra_la_clave(string catalogo, string texto, string clave)
    {
        var json = await _http.GetFromJsonAsync<JsonElement>($"/api/catalogos/{catalogo}?q={Uri.EscapeDataString(texto)}&tamano=100");

        var claves = json.GetProperty("filas").EnumerateArray().Select(f => f.GetProperty("clave").GetString());
        Assert.Contains(clave, claves);
    }

    [Fact]
    public async Task Pagina_los_resultados_y_limita_el_tamano()
    {
        var primera = await _http.GetFromJsonAsync<JsonElement>("/api/catalogos/clave-prod-serv?pagina=1&tamano=10");
        var segunda = await _http.GetFromJsonAsync<JsonElement>("/api/catalogos/clave-prod-serv?pagina=2&tamano=10");
        var enorme = await _http.GetFromJsonAsync<JsonElement>("/api/catalogos/clave-prod-serv?tamano=100000");

        Assert.True(primera.GetProperty("total").GetInt32() > 50_000);
        Assert.Equal(10, primera.GetProperty("filas").GetArrayLength());
        Assert.NotEqual(
            primera.GetProperty("filas")[0].GetProperty("clave").GetString(),
            segunda.GetProperty("filas")[0].GetProperty("clave").GetString());
        Assert.Equal(100, enorme.GetProperty("filas").GetArrayLength()); // el tamaño máximo de página
    }

    [Fact]
    public async Task Los_comodines_de_like_se_buscan_como_texto()
    {
        // Sin escapar, "%" o "_" coincidirían con cualquier cosa.
        var porcentaje = await _http.GetFromJsonAsync<JsonElement>("/api/catalogos/moneda?q=%25");
        var guion = await _http.GetFromJsonAsync<JsonElement>("/api/catalogos/moneda?q=_");

        Assert.Equal(0, porcentaje.GetProperty("total").GetInt32());
        Assert.Equal(0, guion.GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("pagina=abc")]
    [InlineData("tamano=diez")]
    public async Task Un_parametro_que_no_es_numero_da_400_en_espanol(string consulta)
    {
        var respuesta = await _http.GetAsync($"/api/catalogos/moneda?{consulta}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("La solicitud no es válida", problema.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Un_catalogo_que_no_existe_da_404_en_espanol()
    {
        var respuesta = await _http.GetAsync("/api/catalogos/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Catálogo desconocido", problema.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Sembrar_otra_vez_no_duplica_nada()
    {
        var sembrador = api.Services.GetRequiredService<SembradorCatalogos>();
        var antes = await _http.GetFromJsonAsync<JsonElement>("/api/catalogos");

        var resultado = await sembrador.SembrarAsync();

        Assert.False(resultado.Cargado); // la huella de los CSV es la misma: no hay nada que cargar
        var despues = await _http.GetFromJsonAsync<JsonElement>("/api/catalogos");
        Assert.Equal(antes.GetProperty("catalogos").ToString(), despues.GetProperty("catalogos").ToString());
    }

    [Fact]
    public async Task La_documentacion_openapi_describe_los_endpoints()
    {
        var json = await _http.GetFromJsonAsync<JsonElement>("/api/openapi/v1.json");

        var rutas = json.GetProperty("paths");
        Assert.True(rutas.TryGetProperty("/api/catalogos/{nombre}", out _));
        Assert.True(rutas.TryGetProperty("/api/comprobantes/calculo", out _));
    }
}
