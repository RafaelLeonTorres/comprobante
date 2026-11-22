using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Comprobante.Api.Seguridad;

/// <summary>
/// Límite de solicitudes por IP y por minuto, con ventana deslizante: una cubeta para cada tipo de operación (generar,
/// validar, calcular, consultar y dibujar el PDF) y un total para todo lo demás. Al pasarse responde 429 con <c>Retry-After</c>.
/// La API solo se publica en 127.0.0.1 y detrás de nginx, que pasa la IP real en <c>X-Forwarded-For</c>
/// (<c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c> en docker-compose.yml): aquí ya llega como la IP de la conexión.
/// </summary>
internal static class LimiteDeSolicitudes
{
    public const string Generar = "generar";
    public const string Validar = "validar";
    public const string Calcular = "calcular";
    public const string Consultar = "consultar";
    public const string Pdf = "pdf";

    private const string Categoria = "Comprobante.Api.Seguridad.LimiteDeSolicitudes";
    private const int SegmentosPorVentana = 6;
    private static readonly TimeSpan Ventana = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddLimiteDeSolicitudes(this IServiceCollection servicios)
    {
        servicios.AddOptions<LimitesOptions>().BindConfiguration(LimitesOptions.Seccion);

        servicios.AddRateLimiter(opciones =>
        {
            opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opciones.OnRejected = RechazarAsync;

            opciones.AddPolicy(Generar, contexto => PorIp(contexto, Generar, l => l.Generar));
            opciones.AddPolicy(Validar, contexto => PorIp(contexto, Validar, l => l.Validar));
            opciones.AddPolicy(Calcular, contexto => PorIp(contexto, Calcular, l => l.Calcular));
            opciones.AddPolicy(Consultar, contexto => PorIp(contexto, Consultar, l => l.Consultar));
            opciones.AddPolicy(Pdf, contexto => PorIp(contexto, Pdf, l => l.Pdf));

            opciones.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
                contexto.Request.Path.StartsWithSegments("/api/health")
                    ? RateLimitPartition.GetNoLimiter("salud")
                    : PorIp(contexto, "global", l => l.Global));
        });

        return servicios;
    }

    /// <summary>
    /// La IP del cliente como clave de su cuota. Una IPv4 mapeada a IPv6 vale como la IPv4, y una IPv6 se agrupa por su
    /// prefijo /64 (lo que recibe un hogar o un equipo): cambiar de dirección dentro de él no reinicia la cuota.
    /// </summary>
    public static string ClaveDeCliente(IPAddress? ip)
    {
        if (ip is null)
        {
            return "desconocida";
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (ip.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return ip.ToString();
        }

        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }

    private static RateLimitPartition<string> PorIp(
        HttpContext contexto, string cubeta, Func<SolicitudesPorMinuto, int> permitidas)
    {
        var limite = Math.Max(1, permitidas(contexto.RequestServices.GetRequiredService<IOptions<LimitesOptions>>().Value.Solicitudes));
        return RateLimitPartition.GetSlidingWindowLimiter(
            $"{cubeta}:{ClaveDeCliente(contexto.Connection.RemoteIpAddress)}",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = limite,
                Window = Ventana,
                SegmentsPerWindow = SegmentosPorVentana,
                QueueLimit = 0,
            });
    }

    private static async ValueTask RechazarAsync(OnRejectedContext contexto, CancellationToken cancelacion)
    {
        var http = contexto.HttpContext;
        var segundos = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera)
            ? Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds))
            : (int)Ventana.TotalSeconds;

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);

        // Sin la IP ni nada de la solicitud: solo qué ruta se pasó del límite.
        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(Categoria)
            .LogWarning("Límite de solicitudes superado en {Ruta}; reintentar en {Segundos} s", http.Request.Path.Value, segundos);

        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Detail = $"Hiciste demasiadas solicitudes en poco tiempo. Espera {segundos} {(segundos == 1 ? "segundo" : "segundos")} e inténtalo de nuevo.",
            },
        });
    }
}
