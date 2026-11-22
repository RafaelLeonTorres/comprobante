using System.Buffers.Binary;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Comprobante.Pdf;

/// <summary>Lo que se encontró al revisar un logo: si sirve y, si no, qué decirle a quien lo subió.</summary>
public sealed record ResultadoDeLogo
{
    public required bool EsValido { get; init; }

    /// <summary>Qué falló, escrito para quien subió el archivo; nulo si el logo sirve.</summary>
    public string? Error { get; init; }

    /// <summary><c>image/png</c> o <c>image/jpeg</c>, según lo que dicen los primeros bytes del archivo (no su nombre).</summary>
    public string? TipoDeContenido { get; init; }

    public int Ancho { get; init; }

    public int Alto { get; init; }
}

/// <summary>
/// Revisa el logo que sube el usuario antes de ponerlo en el PDF: que sea PNG o JPEG de verdad (por sus primeros bytes, no
/// por la extensión), que no pese más de 200 KB ni mida más de 2,000 × 2,000 px (el tamaño se lee del encabezado, sin
/// decodificar la imagen), y que el motor de PDF la pueda abrir. SVG no se acepta: puede traer referencias externas.
/// </summary>
public static class ValidadorDeLogo
{
    public const int TamanoMaximoEnBytes = 200 * 1024;
    public const int LadoMaximoEnPixeles = 2000;

    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>La revisión completa: la de <see cref="Revisar"/> y, además, que el motor de PDF logre abrir la imagen.</summary>
    public static ResultadoDeLogo Validar(byte[]? logo)
    {
        var revision = Revisar(logo);
        if (!revision.EsValido)
        {
            return revision;
        }

        return SeAbre(logo!) ? revision : Invalido("No se pudo abrir la imagen del logo; guárdala de nuevo como PNG o JPEG.");
    }

    /// <summary>
    /// Las revisiones baratas: peso, tipo y tamaño en píxeles, que se leen de los primeros bytes sin decodificar la imagen.
    /// Sirve para avisar de los errores del formulario sin gastar el trabajo de abrir la imagen, que hace <see cref="Validar"/>.
    /// </summary>
    public static ResultadoDeLogo Revisar(byte[]? logo)
    {
        if (logo is null || logo.Length == 0)
        {
            return Invalido("El archivo del logo está vacío.");
        }

        if (logo.Length > TamanoMaximoEnBytes)
        {
            return Invalido($"El logo pesa más de {TamanoMaximoEnBytes / 1024} KB; sube uno más ligero.");
        }

        string tipo;
        (long ancho, long alto) medidas;
        if (logo.AsSpan().StartsWith(FirmaPng))
        {
            tipo = "image/png";
            medidas = DimensionesDePng(logo);
        }
        else if (logo.Length > 3 && logo[0] == 0xFF && logo[1] == 0xD8 && logo[2] == 0xFF)
        {
            tipo = "image/jpeg";
            medidas = DimensionesDeJpeg(logo);
        }
        else
        {
            return Invalido("El logo debe ser una imagen PNG o JPEG.");
        }

        if (medidas.ancho <= 0 || medidas.alto <= 0)
        {
            return Invalido("No se pudo leer el tamaño de la imagen del logo; guárdala de nuevo como PNG o JPEG.");
        }

        if (medidas.ancho > LadoMaximoEnPixeles || medidas.alto > LadoMaximoEnPixeles)
        {
            return Invalido(
                $"El logo mide {medidas.ancho} × {medidas.alto} px; el máximo es {LadoMaximoEnPixeles} × {LadoMaximoEnPixeles} px.");
        }

        return new ResultadoDeLogo
        {
            EsValido = true,
            TipoDeContenido = tipo,
            Ancho = (int)medidas.ancho,
            Alto = (int)medidas.alto,
        };
    }

    private static ResultadoDeLogo Invalido(string error) => new() { EsValido = false, Error = error };

    /// <summary>PNG: tras la firma de 8 bytes viene el bloque IHDR (longitud, "IHDR", ancho y alto de 4 bytes cada uno).</summary>
    private static (long Ancho, long Alto) DimensionesDePng(byte[] datos)
    {
        if (datos.Length < 24 || !datos.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            return (0, 0);
        }

        return (
            BinaryPrimitives.ReadUInt32BigEndian(datos.AsSpan(16, 4)),
            BinaryPrimitives.ReadUInt32BigEndian(datos.AsSpan(20, 4)));
    }

    /// <summary>JPEG: recorre los marcadores hasta el de cuadro (SOFn), que trae alto y ancho de 2 bytes cada uno.</summary>
    private static (long Ancho, long Alto) DimensionesDeJpeg(byte[] datos)
    {
        var i = 2; // después de la marca de inicio (FF D8)
        while (i + 3 < datos.Length)
        {
            if (datos[i] != 0xFF)
            {
                i++;
                continue;
            }

            var marcador = datos[i + 1];
            if (marcador == 0xFF)
            {
                i++; // relleno
                continue;
            }

            if (marcador is 0x00 or 0x01 or (>= 0xD0 and <= 0xD8))
            {
                i += 2; // marcadores sin longitud
                continue;
            }

            if (marcador is 0xD9 or 0xDA)
            {
                break; // fin de la imagen o inicio de los datos, sin haber visto el tamaño
            }

            var longitud = (datos[i + 2] << 8) | datos[i + 3];
            if (marcador is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                if (longitud < 8 || i + 8 >= datos.Length)
                {
                    break;
                }

                return ((datos[i + 7] << 8) | datos[i + 8], (datos[i + 5] << 8) | datos[i + 6]);
            }

            i += 2 + longitud;
        }

        return (0, 0);
    }

    /// <summary>Intenta dibujar el logo en una hoja de prueba: si el motor no lo puede decodificar, no sirve.</summary>
    private static bool SeAbre(byte[] logo)
    {
        MotorPdf.Iniciar();
        try
        {
            Document.Create(contenedor => contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A6);
                pagina.Content().Image(logo).FitArea();
            })).GeneratePdf();
            return true;
        }
        catch (Exception excepcion) when (excepcion.GetType().Namespace?.StartsWith("QuestPDF", StringComparison.Ordinal) == true)
        {
            return false;
        }
    }
}
