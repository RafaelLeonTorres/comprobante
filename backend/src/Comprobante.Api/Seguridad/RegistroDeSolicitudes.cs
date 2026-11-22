using System.Diagnostics;

namespace Comprobante.Api.Seguridad;

/// <summary>
/// Un renglón por solicitud con lo mínimo para operar la API: método, ruta (sin la consulta), estado, duración y lo que
/// pesó lo que llegó. Nunca el cuerpo, ni las cabeceras, ni la consulta, ni la IP: un CFDI trae RFC, nombres y montos, y
/// lo que no se registra no puede filtrarse. <c>/api/health</c> no se registra, porque Docker lo consulta cada pocos
/// segundos.
/// </summary>
internal sealed class RegistroDeSolicitudes(RequestDelegate siguiente, ILogger<RegistroDeSolicitudes> registro)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        if (contexto.Request.Path.StartsWithSegments("/api/health"))
        {
            await siguiente(contexto);
            return;
        }

        var inicio = Stopwatch.GetTimestamp();
        try
        {
            await siguiente(contexto);
        }
        finally
        {
            registro.LogInformation(
                "{Metodo} {Ruta} -> {Estado} en {Milisegundos:0} ms, {Bytes} bytes recibidos",
                contexto.Request.Method,
                contexto.Request.Path.Value,
                contexto.Response.StatusCode,
                Stopwatch.GetElapsedTime(inicio).TotalMilliseconds,
                contexto.Request.ContentLength ?? 0);
        }
    }
}
