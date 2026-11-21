using Comprobante.Core.Calculo;
using Comprobante.Core.Generacion;
using Comprobante.Core.Sello;
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

        grupo.MapPost("/", GenerarAsync)
            .WithSummary("Arma, sella y revisa un CFDI 4.0 de ingreso o de egreso, sin timbrarlo.")
            .WithDescription(
                "Recibe el comprobante en JSON (el mismo de /calculo, con el emisor de uno de los certificados de prueba del SAT) "
                + "y devuelve el XML sellado con ese certificado, su cadena original, el sello, el cálculo y el reporte de la "
                + "validación del XML generado. El XML no está timbrado: no tiene validez fiscal. Que el reporte traiga hallazgos "
                + "no es un error de la solicitud; solo se responde 400 cuando falta algo para poder armarlo.")
            .Produces<ResultadoGeneracion>()
            .ProducesValidationProblem();

        rutas.MapGet("/api/emisores-prueba", ListarEmisoresDePrueba)
            .WithTags("Comprobantes")
            .WithSummary("Lista los emisores de los certificados de prueba del SAT, los únicos con los que se sella.")
            .WithDescription(
                "Son diez certificados públicos que el SAT publica para probar el timbrado. De cada uno se devuelve el RFC, el "
                + "nombre, el código postal y el régimen fiscal registrados para ese RFC, el número de certificado y su vigencia. "
                + "La llave privada y su contraseña no se exponen por la API.")
            .Produces<IReadOnlyList<EmisorDePruebaDto>>();
    }

    private static async Task<IResult> CalcularAsync(
        SolicitudCfdi? solicitud, ServicioCalculo servicio, CancellationToken cancelacion)
    {
        var resultado = await servicio.CalcularAsync(solicitud, cancelacion);
        return resultado.EsCorrecto ? Results.Ok(resultado.Valor) : ErroresDeSolicitud.AProblema(resultado.Errores);
    }

    private static async Task<IResult> GenerarAsync(
        SolicitudCfdi? solicitud, ServicioGeneracion servicio, CancellationToken cancelacion)
    {
        var resultado = await servicio.GenerarAsync(solicitud, cancelacion);
        return resultado.EsCorrecto ? Results.Ok(resultado.Valor) : ErroresDeSolicitud.AProblema(resultado.Errores);
    }

    private static IResult ListarEmisoresDePrueba(EmisoresDePrueba emisores) =>
        Results.Ok(emisores.Todos.Select(emisor =>
        {
            var certificado = emisores.Certificado(emisor.Rfc)!;
            return new EmisorDePruebaDto(
                emisor.Rfc,
                emisor.Nombre,
                emisor.Persona,
                emisor.CodigoPostal,
                emisor.RegimenFiscal,
                certificado.NoCertificado,
                certificado.ValidoDesde,
                certificado.ValidoHasta);
        }).ToList());
}

/// <summary>Un emisor de prueba tal como lo ve quien consume la API: sin llave ni contraseña.</summary>
/// <param name="Persona">"moral" o "fisica".</param>
internal sealed record EmisorDePruebaDto(
    string Rfc,
    string Nombre,
    string Persona,
    string CodigoPostal,
    string RegimenFiscal,
    string NoCertificado,
    DateTimeOffset ValidoDesde,
    DateTimeOffset ValidoHasta);
