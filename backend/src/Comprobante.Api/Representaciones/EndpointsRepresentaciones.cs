using System.Text;
using System.Xml;
using System.Xml.Linq;
using Comprobante.Api.Comprobantes;
using Comprobante.Api.Seguridad;
using Comprobante.Core;
using Comprobante.Core.Xml;
using Comprobante.Pdf;
using Microsoft.Extensions.Primitives;

namespace Comprobante.Api.Representaciones;

internal static class EndpointsRepresentaciones
{
    /// <summary>
    /// Lo más que pesa el XML que se dibuja (1 MB, la mitad de lo que se puede validar): un CFDI normal ocupa unos cuantos KB
    /// y cada concepto de más son renglones y hojas de más, así que el tamaño del XML es también el tope del trabajo.
    /// </summary>
    internal const int TamanoMaximoDelXmlEnBytes = 1024 * 1024;

    /// <summary>Lo más que pesa el formulario completo: el XML, el logo y lo que ocupan los encabezados de cada parte.</summary>
    internal const int TamanoMaximoDelFormularioEnBytes = TamanoMaximoDelXmlEnBytes + ValidadorDeLogo.TamanoMaximoEnBytes + (64 * 1024);

    public static void MapRepresentaciones(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/representaciones").WithTags("Representación impresa");

        grupo.MapPost("/pdf", GenerarPdfAsync)
            .Accepts<FormularioDelPdf>("multipart/form-data")
            .WithSummary("Dibuja la representación impresa de un CFDI 4.0 en PDF.")
            .WithDescription(
                "Recibe un formulario multipart/form-data con el campo xml (el XML del comprobante, como archivo o como texto, en "
                + "UTF-8 y de hasta 1 MB), el campo opcional logo (un archivo PNG o JPEG de hasta 200 KB y 2,000 × 2,000 px; sin "
                + "él se pone el de la app) y el campo opcional incluirCadenaYSellos (true por omisión; con false se omiten la "
                + "cadena original y los sellos, y queda solo el código QR de un comprobante timbrado) y responde el PDF para "
                + "descargar. Dibuja lo que dice el XML, sin revisar sus reglas: para eso está POST /api/validaciones. Un "
                + "comprobante sin timbre sale marcado como de prueba y sin validez fiscal; uno timbrado trae el código QR de "
                + "verificación del SAT. El PDF va etiquetado para lectores de pantalla (PDF/UA) y no se guarda en el servidor.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(LimiteDeSolicitudes.Pdf);
    }

    private static async Task<IResult> GenerarPdfAsync(
        HttpRequest peticion, HttpResponse respuesta, ServicioDePdf servicio, CancellationToken cancelacion)
    {
        if (!peticion.HasFormContentType
            || !peticion.ContentType!.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                title: "Tipo de contenido no admitido",
                detail: "Envía el XML como formulario multipart/form-data, con el campo xml y, si quieres, logo e incluirCadenaYSellos.",
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        if (peticion.ContentLength > TamanoMaximoDelFormularioEnBytes)
        {
            return DemasiadoGrande();
        }

        IFormCollection formulario;
        try
        {
            formulario = await peticion.ReadFormAsync(cancelacion);
        }
        catch (InvalidDataException excepcion) when (excepcion.Message.Contains("limit", StringComparison.OrdinalIgnoreCase))
        {
            return DemasiadoGrande();
        }
        catch (InvalidDataException)
        {
            return Results.Problem(
                title: "La solicitud no es válida",
                detail: "El formulario no está bien armado: revisa el multipart/form-data y su boundary.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (BadHttpRequestException excepcion) when (excepcion.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return DemasiadoGrande();
        }
        catch (IOException)
        {
            // Un multipart que se acaba antes de su cierre.
            return Results.Problem(
                title: "La solicitud no es válida",
                detail: "El formulario está incompleto o mal armado: revisa el multipart/form-data y su boundary.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var errores = new Dictionary<string, string[]>();

        var texto = await LeerXmlAsync(formulario, cancelacion);
        if (texto is null)
        {
            return DemasiadoGrande();
        }

        XDocument? documento = null;
        if (string.IsNullOrWhiteSpace(texto))
        {
            errores["xml"] = ["Falta el XML del comprobante: envíalo en el campo xml."];
        }
        else
        {
            try
            {
                documento = LectorXmlSeguro.Cargar(texto, TamanoMaximoDelXmlEnBytes);
            }
            catch (XmlException excepcion)
            {
                errores["xml"] = [LectorXmlSeguro.Explicar(excepcion)];
            }

            if (documento is not null && !EsCfdi40(documento))
            {
                documento = null;
                errores["xml"] = ["Este XML no es un CFDI 4.0: el nodo raíz debe ser cfdi:Comprobante con Version=\"4.0\"."];
            }
        }

        var logo = await LeerLogoAsync(formulario, errores, cancelacion);
        var incluirCadenaYSellos = LeerIncluirCadenaYSellos(formulario, errores);

        if (errores.Count > 0)
        {
            return Results.ValidationProblem(errores, title: "Hay datos que corregir", detail: "Revisa los campos marcados y vuelve a intentarlo.");
        }

        var resultado = await servicio.GenerarAsync(documento!, logo, incluirCadenaYSellos, cancelacion);
        switch (resultado)
        {
            case ResultadoDePdf.Listo listo:
                // Tiene datos de una persona: que ningún intermediario lo guarde.
                respuesta.Headers.CacheControl = "no-store";
                return Results.File(listo.Contenido, "application/pdf", listo.NombreDeArchivo);

            case ResultadoDePdf.LogoNoValido invalido:
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["logo"] = [invalido.Error] },
                    title: "Hay datos que corregir",
                    detail: "Revisa los campos marcados y vuelve a intentarlo.");

            case ResultadoDePdf.Ocupado:
                respuesta.Headers.RetryAfter = "3";
                return Results.Problem(
                    title: "Demasiadas solicitudes",
                    detail: "Se están dibujando muchos PDF a la vez. Espera unos segundos e inténtalo de nuevo.",
                    statusCode: StatusCodes.Status429TooManyRequests);

            default:
                return Results.Problem(
                    title: "No se pudo generar el PDF",
                    detail: "El comprobante es tan grande o tan raro que no se pudo acomodar en las hojas. Prueba sin la cadena original y los sellos.",
                    statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static IResult DemasiadoGrande() =>
        Results.Problem(
            title: "El contenido es demasiado grande",
            detail: $"El XML pesa más de {TamanoMaximoDelXmlEnBytes / (1024 * 1024)} MB o el logo más de {ValidadorDeLogo.TamanoMaximoEnBytes / 1024} KB: "
                + "un CFDI normal ocupa unos cuantos KB.",
            statusCode: StatusCodes.Status413PayloadTooLarge);

    private static bool EsCfdi40(XDocument documento) =>
        documento.Root is { } raiz
        && raiz.Name == XNamespace.Get(EscritorCfdi.EspacioCfdi) + "Comprobante"
        && raiz.Attribute("Version")?.Value == "4.0";

    /// <summary>El XML del campo xml, sea archivo o texto; cadena vacía si no vino y nulo si pesa más de lo permitido.</summary>
    private static async Task<string?> LeerXmlAsync(IFormCollection formulario, CancellationToken cancelacion)
    {
        if (formulario.Files.GetFile("xml") is { } archivo)
        {
            if (archivo.Length > TamanoMaximoDelXmlEnBytes)
            {
                return null;
            }

            using var lector = new StreamReader(archivo.OpenReadStream(), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            return await lector.ReadToEndAsync(cancelacion);
        }

        var valor = formulario["xml"].ToString();
        return Encoding.UTF8.GetByteCount(valor) > TamanoMaximoDelXmlEnBytes ? null : valor;
    }

    /// <summary>El logo, si vino; un campo logo que no es un archivo se reporta como error.</summary>
    private static async Task<byte[]?> LeerLogoAsync(
        IFormCollection formulario, Dictionary<string, string[]> errores, CancellationToken cancelacion)
    {
        if (formulario.Files.GetFile("logo") is not { } archivo)
        {
            if (!StringValues.IsNullOrEmpty(formulario["logo"]))
            {
                errores["logo"] = ["El logo debe enviarse como un archivo PNG o JPEG."];
            }

            return null;
        }

        if (archivo.Length == 0)
        {
            return null; // un campo de archivo que se dejó sin elegir
        }

        using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria, cancelacion);
        var logo = memoria.ToArray();

        // Lo barato se revisa aquí para que el error del logo llegue junto con los demás; abrir la imagen es trabajo del
        // servicio, que solo lo hace cuando hay un lugar libre.
        if (ValidadorDeLogo.Revisar(logo) is { EsValido: false } invalido)
        {
            errores["logo"] = [invalido.Error!];
            return null;
        }

        return logo;
    }

    private static bool LeerIncluirCadenaYSellos(IFormCollection formulario, Dictionary<string, string[]> errores)
    {
        var valor = formulario["incluirCadenaYSellos"].ToString().Trim();
        if (valor.Length == 0)
        {
            return true;
        }

        if (bool.TryParse(valor, out var incluir))
        {
            return incluir;
        }

        switch (valor)
        {
            case "1":
                return true;
            case "0":
                return false;
            default:
                errores["incluirCadenaYSellos"] = ["Debe ser true o false."];
                return true;
        }
    }
}

/// <summary>Los campos del formulario de <c>POST /api/representaciones/pdf</c>; solo documenta la API, el endpoint lee el formulario a mano.</summary>
/// <param name="Xml">El XML del comprobante (CFDI 4.0), como archivo o como texto.</param>
/// <param name="Logo">Un PNG o JPEG de hasta 200 KB y 2,000 × 2,000 px para el encabezado.</param>
/// <param name="IncluirCadenaYSellos">Si el PDF lleva la cadena original y los sellos; true por omisión.</param>
internal sealed record FormularioDelPdf(IFormFile Xml, IFormFile? Logo, bool? IncluirCadenaYSellos);
