using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Comprobante.Api.Representaciones;

/// <summary>
/// El generador de la documentación no sabe que <c>Results.File</c> responde un PDF: deja el 200 sin contenido. Aquí se le
/// dice que es un archivo <c>application/pdf</c>, para que la documentación y quien la lea (Scalar) lo muestren así.
/// </summary>
internal static class DocumentacionDelPdf
{
    public static OpenApiOptions DescribirElPdf(this OpenApiOptions opciones) =>
        opciones.AddOperationTransformer((operacion, contexto, _) =>
        {
            if (contexto.Description.RelativePath == "api/representaciones/pdf"
                && operacion.Responses is { } respuestas
                && respuestas.TryGetValue("200", out var correcta)
                && correcta is OpenApiResponse respuesta)
            {
                respuesta.Description = "El PDF de la representación impresa, para descargar.";
                respuesta.Content = new Dictionary<string, IOpenApiMediaType>
                {
                    ["application/pdf"] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } },
                };
            }

            return Task.CompletedTask;
        });
}
