using Comprobante.Core.Calculo;
using Comprobante.Core.Solicitud;

namespace Comprobante.Api.Comprobantes;

internal static class EndpointsComprobantes
{
    public static void MapComprobantes(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/comprobantes").WithTags("Comprobantes");

        grupo.MapPost("/calculo", CalcularAsync)
            .WithSummary("Calcula importes, impuestos y totales de un comprobante (vista previa).")
            .WithDescription(
                "Recibe el comprobante en JSON y devuelve el importe de cada concepto, sus impuestos, el resumen de impuestos y los "
                + "totales, con los redondeos del Anexo 20: cada concepto con hasta seis decimales y el redondeo al final, a los "
                + "decimales de la moneda. No guarda nada. Si hay datos que impiden calcular, responde 400 con los errores por campo.")
            .Produces<ResultadoCalculo>()
            .ProducesValidationProblem();
    }

    private static async Task<IResult> CalcularAsync(
        SolicitudCfdi? solicitud, ServicioCalculo servicio, CancellationToken cancelacion)
    {
        var resultado = await servicio.CalcularAsync(solicitud, cancelacion);
        return resultado.EsCorrecto ? Results.Ok(resultado.Valor) : ErroresDeSolicitud.AProblema(resultado.Errores);
    }
}
