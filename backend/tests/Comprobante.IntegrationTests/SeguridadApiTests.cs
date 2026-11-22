using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Comprobante.Api.Seguridad;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace Comprobante.IntegrationTests;

/// <summary>
/// Los límites de la API: solicitudes por IP (429), tamaño del JSON (413) y registros sin contenido. Cada prueba arranca su
/// propia copia de la API, con los límites que necesita, contra la misma base. Que el XML con una entidad externa o una
/// bomba de entidades no filtre nada ni tarde, y los 413 y 415 del XML, se prueban en <see cref="ValidacionesApiTests"/>.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public sealed class SeguridadApiTests(ApiFactory api) : IDisposable
{
    private const string Emisores = "/api/emisores-prueba";

    private readonly List<WebApplicationFactory<Program>> _copias = [];

    public void Dispose()
    {
        foreach (var copia in _copias)
        {
            copia.Dispose();
        }
    }

    private WebApplicationFactory<Program> Copia(Action<IWebHostBuilder> configurar)
    {
        var copia = api.WithWebHostBuilder(configurar);
        _copias.Add(copia);
        return copia;
    }

    private HttpClient ClienteConLimites(params (string Clave, string Valor)[] limites) =>
        Copia(constructor =>
        {
            foreach (var (clave, valor) in limites)
            {
                constructor.UseSetting($"Limites:{clave}", valor);
            }
        }).CreateClient();

    private static HttpRequestMessage Pedir(HttpMethod metodo, string ruta, string ip = "203.0.113.10")
    {
        var peticion = new HttpRequestMessage(metodo, ruta);
        peticion.Headers.Add("X-Forwarded-For", ip);
        return peticion;
    }

    private static async Task<HttpStatusCode> EstadoAsync(HttpClient http, string ruta, string ip = "203.0.113.10")
    {
        using var respuesta = await http.SendAsync(Pedir(HttpMethod.Get, ruta, ip));
        return respuesta.StatusCode;
    }

    [Fact]
    public async Task Al_pasar_el_limite_responde_429_con_Retry_After_y_el_problema_en_espanol()
    {
        var http = ClienteConLimites(("Solicitudes:Consultar", "3"));

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, await EstadoAsync(http, Emisores));
        }

        using var respuesta = await http.SendAsync(Pedir(HttpMethod.Get, Emisores));

        Assert.Equal(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var espera = int.Parse(respuesta.Headers.GetValues("Retry-After").Single());
        Assert.InRange(espera, 1, 60);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(429, problema.GetProperty("status").GetInt32());
        Assert.Equal("Demasiadas solicitudes", problema.GetProperty("title").GetString());
        Assert.Contains("Espera", problema.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cada_IP_tiene_su_propia_cuota()
    {
        var http = ClienteConLimites(("Solicitudes:Consultar", "2"));

        Assert.Equal(HttpStatusCode.OK, await EstadoAsync(http, Emisores, "198.51.100.1"));
        Assert.Equal(HttpStatusCode.OK, await EstadoAsync(http, Emisores, "198.51.100.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await EstadoAsync(http, Emisores, "198.51.100.1"));

        Assert.Equal(HttpStatusCode.OK, await EstadoAsync(http, Emisores, "198.51.100.2"));
    }

    [Fact]
    public async Task Las_direcciones_de_un_mismo_prefijo_IPv6_comparten_cuota()
    {
        var http = ClienteConLimites(("Solicitudes:Consultar", "1"));

        Assert.Equal(HttpStatusCode.OK, await EstadoAsync(http, Emisores, "2001:db8:abcd:12::1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await EstadoAsync(http, Emisores, "2001:db8:abcd:12:ffff:ffff:ffff:ffff"));
        Assert.Equal(HttpStatusCode.OK, await EstadoAsync(http, Emisores, "2001:db8:abcd:13::1"));
    }

    [Fact]
    public async Task Cada_tipo_de_operacion_tiene_su_cubeta()
    {
        var http = ClienteConLimites(("Solicitudes:Consultar", "1"));
        Assert.Equal(HttpStatusCode.OK, await EstadoAsync(http, Emisores));
        Assert.Equal(HttpStatusCode.TooManyRequests, await EstadoAsync(http, Emisores));

        // Agotar las consultas no toca la cubeta de validar: llega al endpoint, que rechaza el tipo de contenido (415).
        using var peticion = Pedir(HttpMethod.Post, "/api/validaciones");
        peticion.Content = new StringContent("<a/>", Encoding.UTF8, "text/plain");
        using var validar = await http.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, validar.StatusCode);
    }

    [Fact]
    public async Task El_limite_global_cuenta_todas_las_solicitudes_de_la_IP()
    {
        var http = ClienteConLimites(("Solicitudes:Global", "2"));

        Assert.Equal(HttpStatusCode.OK, await EstadoAsync(http, Emisores));
        Assert.Equal(HttpStatusCode.OK, await EstadoAsync(http, "/api/catalogos"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await EstadoAsync(http, "/api/validaciones/reglas"));
    }

    [Fact]
    public async Task Health_no_tiene_limite_ni_gasta_el_de_los_demas()
    {
        var http = ClienteConLimites(("Solicitudes:Global", "1"));

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.OK, await EstadoAsync(http, "/api/health"));
        }

        Assert.Equal(HttpStatusCode.OK, await EstadoAsync(http, Emisores));
        Assert.Equal(HttpStatusCode.TooManyRequests, await EstadoAsync(http, Emisores));
        Assert.Equal(HttpStatusCode.OK, await EstadoAsync(http, "/api/health"));
    }

    private static string JsonDe(int bytes) => $$"""{"conceptos":[],"relleno":"{{new string('x', bytes)}}"}""";

    private static StringContent Json(string texto) => new(texto, Encoding.UTF8, "application/json");

    [Theory]
    [InlineData("/api/comprobantes/calculo")]
    [InlineData("/api/comprobantes")]
    public async Task Un_json_que_pasa_del_limite_da_413_sin_procesarse(string ruta)
    {
        var http = ClienteConLimites(("JsonMaximoBytes", "2000"));

        using var respuesta = await http.PostAsync(ruta, Json(JsonDe(5000)));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("El contenido es demasiado grande", problema.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Un_json_dentro_del_limite_llega_al_endpoint()
    {
        var http = ClienteConLimites(("JsonMaximoBytes", "2000"));

        using var respuesta = await http.PostAsync("/api/comprobantes/calculo", Json(JsonDe(100)));

        // Sin conceptos que calcular: el endpoint lo rechaza con un 400, no el límite con un 413.
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_limite_del_json_tambien_vale_cuando_el_cuerpo_llega_sin_longitud()
    {
        var http = ClienteConLimites(("JsonMaximoBytes", "2000"));

        using var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/comprobantes/calculo")
        {
            Content = new ContenidoSinLongitud(Encoding.UTF8.GetBytes(JsonDe(5000))),
        };
        using var respuesta = await http.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_json_sin_longitud_dentro_del_limite_se_procesa_completo()
    {
        var http = ClienteConLimites(("JsonMaximoBytes", "2000"));
        var comprobante = JsonSerializer.Serialize(new
        {
            conceptos = new[]
            {
                new
                {
                    claveProdServ = "84111506",
                    claveUnidad = "E48",
                    descripcion = "Servicio de prueba",
                    cantidad = 1,
                    valorUnitario = 100.00m,
                    traslados = new[] { new { impuesto = "002", tipoFactor = "Tasa", tasaOCuota = 0.16m } },
                },
            },
        });

        using var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/comprobantes/calculo")
        {
            Content = new ContenidoSinLongitud(Encoding.UTF8.GetBytes(comprobante)),
        };
        using var respuesta = await http.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var calculo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(116.00m, calculo.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Los_registros_no_llevan_el_contenido_ni_la_consulta_y_health_no_se_registra()
    {
        var registros = new ProveedorDeRegistros();
        var http = Copia(constructor => constructor.ConfigureLogging(registro => registro.AddProvider(registros))).CreateClient();
        const string secreto = "NOMBRE-SECRETO-DEL-RECEPTOR";
        var xml = $"""<cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" Nombre="{secreto}"/>""";

        using var validacion = await http.PostAsync(
            $"/api/validaciones?token={secreto}", new StringContent(xml, Encoding.UTF8, "application/xml"));
        using var salud = await http.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, validacion.StatusCode);
        var mensajes = registros.Mensajes;
        Assert.Contains(mensajes, m => m.Contains("POST /api/validaciones -> 200", StringComparison.Ordinal));
        Assert.DoesNotContain(mensajes, m => m.Contains(secreto, StringComparison.Ordinal));
        Assert.DoesNotContain(mensajes, m => m.Contains("/api/health", StringComparison.Ordinal));
        Assert.DoesNotContain(mensajes, m => m.Contains("203.0.113", StringComparison.Ordinal));
    }

    /// <summary>Un cuerpo que no dice cuánto pesa: viaja en trozos y la API no puede decidir por el encabezado.</summary>
    private sealed class ContenidoSinLongitud : HttpContent
    {
        private readonly byte[] _bytes;

        public ContenidoSinLongitud(byte[] bytes)
        {
            _bytes = bytes;
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        protected override Task SerializeToStreamAsync(Stream flujo, TransportContext? contexto) =>
            flujo.WriteAsync(_bytes, 0, _bytes.Length);

        protected override bool TryComputeLength(out long longitud)
        {
            longitud = 0;
            return false;
        }
    }

    /// <summary>Guarda en memoria lo que registra la API, con los mismos niveles que en producción, para revisarlo.</summary>
    private sealed class ProveedorDeRegistros : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _mensajes = new();

        public IReadOnlyList<string> Mensajes => [.. _mensajes];

        public ILogger CreateLogger(string categoria) => new Registro(_mensajes);

        public void Dispose()
        {
        }

        private sealed class Registro(ConcurrentQueue<string> mensajes) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState estado)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel nivel) => true;

            public void Log<TState>(
                LogLevel nivel, EventId id, TState estado, Exception? excepcion, Func<TState, Exception?, string> formato) =>
                mensajes.Enqueue(excepcion is null ? formato(estado, null) : $"{formato(estado, excepcion)} | {excepcion}");
        }
    }
}

public class ClaveDeClienteTests
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:abcd:12:aaaa:bbbb:cccc:dddd", "2001:db8:abcd:12::/64")]
    public void La_clave_es_la_IPv4_o_el_prefijo_64_de_la_IPv6(string ip, string esperada) =>
        Assert.Equal(esperada, LimiteDeSolicitudes.ClaveDeCliente(IPAddress.Parse(ip)));

    [Fact]
    public void Dos_direcciones_del_mismo_prefijo_comparten_clave_y_las_de_otro_prefijo_no()
    {
        var una = LimiteDeSolicitudes.ClaveDeCliente(IPAddress.Parse("2001:db8:1:2::1"));
        var otra = LimiteDeSolicitudes.ClaveDeCliente(IPAddress.Parse("2001:db8:1:2:9::9"));
        var distinta = LimiteDeSolicitudes.ClaveDeCliente(IPAddress.Parse("2001:db8:1:3::1"));

        Assert.Equal(una, otra);
        Assert.NotEqual(una, distinta);
    }

    [Fact]
    public void Sin_IP_hay_una_clave_comun() =>
        Assert.Equal("desconocida", LimiteDeSolicitudes.ClaveDeCliente(null));
}
