using Comprobante.Core;

namespace Comprobante.Api.Comprobantes;

internal static class ErroresDeSolicitud
{
    /// <summary>400 con los errores agrupados por campo (<c>conceptos[0].cantidad</c>), como los espera la interfaz.</summary>
    public static IResult AProblema(IReadOnlyList<ErrorCampo> errores)
    {
        var porCampo = errores
            .GroupBy(e => e.Campo)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Mensaje).ToArray());

        return Results.ValidationProblem(
            porCampo,
            title: "Hay datos que corregir",
            detail: "Revisa los campos marcados y vuelve a intentarlo.");
    }
}
