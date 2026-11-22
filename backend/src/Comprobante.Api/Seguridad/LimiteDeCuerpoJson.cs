using Microsoft.Extensions.Options;

namespace Comprobante.Api.Seguridad;

/// <summary>
/// Tope del cuerpo del JSON de <c>/api/comprobantes</c> (cálculo y generación): 413 si pesa más de
/// <c>Limites:JsonMaximoBytes</c>. Con <c>Content-Length</c> se decide sin leer nada; un cuerpo sin longitud (en trozos)
/// se lee hasta el tope y se rechaza si lo pasa. Kestrel también tiene su límite, pero este se aplica igual en las pruebas
/// (<c>TestServer</c> no lo respeta) y da la respuesta en español.
/// </summary>
internal sealed class LimiteDeCuerpoJson(RequestDelegate siguiente)
{
    public static bool Aplica(HttpContext contexto) =>
        HttpMethods.IsPost(contexto.Request.Method) && contexto.Request.Path.StartsWithSegments("/api/comprobantes");

    public async Task InvokeAsync(HttpContext contexto, IOptions<LimitesOptions> opciones)
    {
        var limite = opciones.Value.JsonMaximoBytes;
        var peticion = contexto.Request;

        if (peticion.ContentLength > limite)
        {
            await DemasiadoGrande(contexto, limite);
            return;
        }

        if (peticion.ContentLength is null)
        {
            var cuerpo = await LeerConTopeAsync(peticion.Body, limite, contexto.RequestAborted);
            if (cuerpo is null)
            {
                await DemasiadoGrande(contexto, limite);
                return;
            }

            peticion.Body = cuerpo;
        }

        await siguiente(contexto);
    }

    /// <summary>Lee el cuerpo completo hasta <paramref name="limite"/> bytes; nulo si lo pasa (y deja de leer).</summary>
    private static async Task<MemoryStream?> LeerConTopeAsync(Stream origen, int limite, CancellationToken cancelacion)
    {
        var memoria = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int leidos;
        while ((leidos = await origen.ReadAsync(buffer, cancelacion)) > 0)
        {
            if (memoria.Length + leidos > limite)
            {
                await memoria.DisposeAsync();
                return null;
            }

            memoria.Write(buffer, 0, leidos);
        }

        memoria.Position = 0;
        return memoria;
    }

    private static Task DemasiadoGrande(HttpContext contexto, int limite) =>
        Results.Problem(
                title: "El contenido es demasiado grande",
                detail: $"El JSON pesa más de {limite / 1024} KB: un comprobante normal ocupa unos cuantos KB.",
                statusCode: StatusCodes.Status413PayloadTooLarge)
            .ExecuteAsync(contexto);
}
