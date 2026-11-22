using System.Net.Http.Headers;
using System.Text;
using Comprobante.Api.Seguridad;
using Comprobante.Catalogos;
using Comprobante.Core.Validacion;
using Comprobante.Core.Xml;

namespace Comprobante.Api.Validaciones;

internal static class EndpointsValidaciones
{
    /// <summary>Lo que se acepta de un XML (2 MB): el mismo límite con el que lo lee el validador y que pone nginx.</summary>
    internal const int TamanoMaximoEnBytes = LectorXmlSeguro.TamanoMaximoPorOmision;

    public static void MapValidaciones(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/validaciones").WithTags("Validaciones");

        grupo.MapPost("/", ValidarAsync)
            .Accepts<string>("application/xml", "text/xml")
            .WithSummary("Valida un XML de CFDI 4.0: esquema, reglas de la matriz de errores del SAT y sello.")
            .WithDescription(
                "Recibe el XML tal cual (Content-Type application/xml o text/xml, en UTF-8, de hasta 2 MB) y devuelve el reporte: "
                + "cada hallazgo con el código del SAT (CFDI40161...) o propio (CMP...), el nodo y el atributo que fallan, la línea "
                + "del XML y, si se puede calcular, el valor esperado; además la cadena original, si el sello verifica y los datos "
                + "del certificado. Que el XML traiga hallazgos no es un error de la solicitud: se responde 200 con el reporte. "
                + "Las reglas que dependen de datos del SAT que la app no tiene sin conexión (lista de RFC inscritos, tipo de "
                + "cambio oficial...) no se revisan: GET /api/validaciones/reglas dice cuáles.")
            .Produces<ReporteValidacion>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(LimiteDeSolicitudes.Validar);

        grupo.MapGet("/reglas", ListarReglas)
            .WithSummary("Lista las 129 reglas de la matriz de errores del SAT y cuáles revisa esta app.")
            .WithDescription(
                "Cada regla trae su código, el nodo y el atributo, la descripción del SAT y su estado: revisada, parcial (se revisa "
                + "una parte, la nota dice cuál) o no revisada (la nota dice por qué no se puede sin conexión).")
            .Produces<CoberturaDeReglas>()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(LimiteDeSolicitudes.Consultar);
    }

    private static async Task<IResult> ValidarAsync(
        HttpRequest peticion, ValidadorCfdi validador, CancellationToken cancelacion)
    {
        if (!MediaTypeHeaderValue.TryParse(peticion.ContentType, out var tipo)
            || !(string.Equals(tipo.MediaType, "application/xml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tipo.MediaType, "text/xml", StringComparison.OrdinalIgnoreCase)))
        {
            return Results.Problem(
                title: "Tipo de contenido no admitido",
                detail: "Envía el XML como cuerpo de la solicitud con el Content-Type application/xml o text/xml.",
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        if (peticion.ContentLength > TamanoMaximoEnBytes)
        {
            return DemasiadoGrande();
        }

        var xml = await LeerAsync(peticion.Body, Codificacion(tipo.CharSet), cancelacion);
        if (xml is null)
        {
            return DemasiadoGrande();
        }

        if (string.IsNullOrWhiteSpace(xml))
        {
            return Results.Problem(
                title: "La solicitud no es válida",
                detail: "El cuerpo está vacío: envía el XML del CFDI que quieres validar.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(await validador.ValidarAsync(xml, cancelacion));
    }

    private static IResult DemasiadoGrande() =>
        Results.Problem(
            title: "El contenido es demasiado grande",
            detail: $"El XML pesa más de {TamanoMaximoEnBytes / (1024 * 1024)} MB: un CFDI normal ocupa unos cuantos KB.",
            statusCode: StatusCodes.Status413PayloadTooLarge);

    /// <summary>UTF-8 salvo que el Content-Type pida otra codificación de las que .NET trae; el CFDI debe ir en UTF-8.</summary>
    private static Encoding Codificacion(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return new UTF8Encoding(false);
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"'));
        }
        catch (ArgumentException)
        {
            return new UTF8Encoding(false);
        }
    }

    /// <summary>Lee el cuerpo hasta el máximo permitido; nulo si lo pasa (no se sigue leyendo).</summary>
    private static async Task<string?> LeerAsync(Stream cuerpo, Encoding codificacion, CancellationToken cancelacion)
    {
        using var memoria = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int leidos;
        while ((leidos = await cuerpo.ReadAsync(buffer, cancelacion)) > 0)
        {
            if (memoria.Length + leidos > TamanoMaximoEnBytes)
            {
                return null;
            }

            memoria.Write(buffer, 0, leidos);
        }

        memoria.Position = 0;
        using var lector = new StreamReader(memoria, codificacion, detectEncodingFromByteOrderMarks: true);
        return await lector.ReadToEndAsync(cancelacion);
    }

    private static IResult ListarReglas()
    {
        var reglas = MatrizDeErrores.Todos
            .Select(fila =>
            {
                var regla = ReglasSat.Buscar(fila.Codigo)
                    ?? throw new InvalidOperationException($"La regla {fila.Codigo} de la matriz no está en la lista de cobertura.");
                return new ReglaDto(
                    fila.Codigo,
                    fila.Elemento,
                    string.IsNullOrEmpty(fila.Atributo) ? null : fila.Atributo,
                    fila.Descripcion,
                    regla.Estado,
                    regla.Nota,
                    regla.CubiertaPor);
            })
            .ToList();

        return Results.Ok(new CoberturaDeReglas(
            new ResumenDeCobertura(
                reglas.Count,
                reglas.Count(r => r.Estado == EstadoDeRegla.Revisada),
                reglas.Count(r => r.Estado == EstadoDeRegla.Parcial),
                reglas.Count(r => r.Estado == EstadoDeRegla.NoRevisada)),
            reglas));
    }
}

/// <summary>Las reglas de la matriz de errores y cuántas se revisan.</summary>
internal sealed record CoberturaDeReglas(ResumenDeCobertura Resumen, IReadOnlyList<ReglaDto> Reglas);

internal sealed record ResumenDeCobertura(int Total, int Revisadas, int Parciales, int NoRevisadas);

/// <summary>Una regla del SAT con lo que hace la app con ella.</summary>
/// <param name="Elemento">El nodo del CFDI al que corresponde, por ejemplo <c>cfdi:Receptor</c>.</param>
/// <param name="Atributo">El atributo que revisa; nulo si la regla es del nodo completo.</param>
/// <param name="Nota">Qué se revisa exactamente o por qué no se puede revisar sin conexión.</param>
/// <param name="CubiertaPor">Si comparte condición con otra regla y se informa con el código de esa.</param>
internal sealed record ReglaDto(
    string Codigo,
    string Elemento,
    string? Atributo,
    string Descripcion,
    EstadoDeRegla Estado,
    string? Nota,
    string? CubiertaPor);
