using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;

namespace Comprobante.Api.Errores;

/// <summary>
/// Los errores de la API van como <c>ProblemDetails</c> (RFC 9457) con el título en español, también los que genera el
/// propio framework (JSON mal formado, ruta que no existe, método no permitido...). Los de JSON, que el framework escribe
/// en inglés y con detalles internos (<c>System.Decimal</c>, <c>BytePositionInLine</c>), se reescriben aquí.
/// </summary>
internal static partial class ProblemasEnEspanol
{
    // Título que el framework pone a los 400 de validación; los que escribe esta API ya vienen en español.
    private const string TituloDeValidacionDelFramework = "One or more validation errors occurred.";

    private static readonly Dictionary<int, string> Titulos = new()
    {
        [StatusCodes.Status400BadRequest] = "La solicitud no es válida",
        [StatusCodes.Status404NotFound] = "No se encontró lo que pides",
        [StatusCodes.Status405MethodNotAllowed] = "Método no permitido",
        [StatusCodes.Status406NotAcceptable] = "Formato de respuesta no disponible",
        [StatusCodes.Status413PayloadTooLarge] = "El contenido es demasiado grande",
        [StatusCodes.Status415UnsupportedMediaType] = "Tipo de contenido no admitido",
        [StatusCodes.Status429TooManyRequests] = "Demasiadas solicitudes",
        [StatusCodes.Status500InternalServerError] = "Ocurrió un error al procesar la solicitud",
    };

    public static IServiceCollection AddProblemasEnEspanol(this IServiceCollection servicios) =>
        servicios.AddProblemDetails(opciones => opciones.CustomizeProblemDetails = contexto =>
        {
            var problema = contexto.ProblemDetails;
            var estado = problema.Status ?? contexto.HttpContext.Response.StatusCode;
            problema.Status = estado;

            if (problema is HttpValidationProblemDetails { Title: TituloDeValidacionDelFramework } validacion)
            {
                validacion.Title = Titulos[StatusCodes.Status400BadRequest];
                TraducirErrores(validacion.Errors);
            }
            else if (EsTituloDelFramework(problema.Title, estado) && Titulos.TryGetValue(estado, out var titulo))
            {
                problema.Title = titulo;
            }

            if (estado == StatusCodes.Status400BadRequest && string.IsNullOrEmpty(problema.Detail))
            {
                problema.Detail = "Revisa lo que enviaste: el formato no es el esperado (por ejemplo, un JSON mal formado o un número que no lo es).";
            }
        });

    private static bool EsTituloDelFramework(string? titulo, int estado) =>
        titulo is null
        || titulo == ReasonPhrases.GetReasonPhrase(estado)
        || titulo.StartsWith("An error occurred", StringComparison.Ordinal);

    /// <summary>
    /// El framework nombra el campo con su ruta JSON (<c>$.conceptos[0].cantidad</c>); aquí se deja como lo nombra el resto
    /// de la API (<c>conceptos[0].cantidad</c>), y el cuerpo en conjunto va con la clave vacía, que es la convención.
    /// </summary>
    private static void TraducirErrores(IDictionary<string, string[]> errores)
    {
        var traducidos = new Dictionary<string, List<string>>();
        foreach (var (clave, mensajes) in errores)
        {
            var campo = clave == "$" ? string.Empty : clave.StartsWith("$.", StringComparison.Ordinal) ? clave[2..] : clave;
            if (!traducidos.TryGetValue(campo, out var lista))
            {
                traducidos[campo] = lista = [];
            }

            lista.AddRange(mensajes.Select(m => TraducirMensaje(campo, m)));
        }

        errores.Clear();
        foreach (var (campo, lista) in traducidos)
        {
            errores[campo] = [.. lista];
        }
    }

    private static string TraducirMensaje(string campo, string mensaje)
    {
        if (mensaje.Contains("could not be converted to", StringComparison.Ordinal))
        {
            if (campo.Length == 0)
            {
                return "El cuerpo debe ser un objeto JSON con los datos del comprobante.";
            }

            var tipo = TipoEsperado().Match(mensaje).Groups["tipo"].Value;
            return tipo switch
            {
                _ when tipo.Contains("Decimal") || tipo.Contains("Double") || tipo.Contains("Single") || tipo.Contains("Int") =>
                    "El valor debe ser un número.",
                _ when tipo.Contains("Boolean") => "El valor debe ser verdadero o falso.",
                _ when tipo.Contains("DateTime") => "El valor debe ser una fecha con el formato 2026-01-31T12:00:00.",
                _ => "El valor no tiene el formato esperado.",
            };
        }

        var posicion = PosicionEnElJson().Match(mensaje);
        if (posicion.Success)
        {
            // El framework cuenta desde cero; quien escribió el JSON, desde uno.
            var linea = int.Parse(posicion.Groups["linea"].Value) + 1;
            var columna = int.Parse(posicion.Groups["columna"].Value) + 1;
            return $"El cuerpo no es un JSON válido (línea {linea}, posición {columna}): revisa comas, comillas y llaves.";
        }

        return mensaje;
    }

    [GeneratedRegex(@"could not be converted to (?<tipo>[^\s|]+?)\.? Path:", RegexOptions.CultureInvariant)]
    private static partial Regex TipoEsperado();

    [GeneratedRegex(@"LineNumber: (?<linea>\d+) \| BytePositionInLine: (?<columna>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex PosicionEnElJson();
}
