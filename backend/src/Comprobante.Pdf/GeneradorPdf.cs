using Comprobante.Core.Representacion;
using QuestPDF.Fluent;

namespace Comprobante.Pdf;

/// <summary>Lo que el usuario puede cambiar del PDF: su logo y si lleva la cadena original y los sellos.</summary>
public sealed record OpcionesDelPdf
{
    /// <summary>
    /// El logo del emisor, PNG o JPEG ya revisado con <see cref="ValidadorDeLogo"/>. Nulo imprime el de Comprobante.
    /// </summary>
    public byte[]? Logo { get; init; }

    /// <summary>
    /// Si se imprimen la cadena original y los sellos digitales. Sin ellos el PDF queda más corto: con timbre solo lleva el
    /// código QR, y sin timbre no lleva nada de esa sección.
    /// </summary>
    public bool IncluirCadenaYSellos { get; init; } = true;
}

/// <summary>El PDF no se pudo dibujar (un comprobante tan largo o raro que no cabe en el diseño, o un logo que no se abre).</summary>
public sealed class PdfNoGenerableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>Dibuja en un PDF la representación impresa que armó <see cref="ConstructorDeRepresentacion"/>.</summary>
public static class GeneradorPdf
{
    /// <summary>
    /// El PDF de la representación impresa, etiquetado para lectores de pantalla (PDF/UA) y con las fuentes incrustadas.
    /// </summary>
    /// <exception cref="PdfNoGenerableException">El diseño no pudo dibujar este comprobante.</exception>
    public static byte[] Generar(RepresentacionImpresa representacion, OpcionesDelPdf? opciones = null)
    {
        ArgumentNullException.ThrowIfNull(representacion);

        MotorPdf.Iniciar();
        try
        {
            return new PlantillaDeRepresentacion(representacion, opciones ?? new OpcionesDelPdf()).Armar().GeneratePdf();
        }
        catch (Exception excepcion) when (EsDeQuestPdf(excepcion))
        {
            throw new PdfNoGenerableException("No se pudo dibujar el PDF de este comprobante.", excepcion);
        }
    }

    private static bool EsDeQuestPdf(Exception excepcion) =>
        excepcion.GetType().Namespace?.StartsWith("QuestPDF", StringComparison.Ordinal) == true;
}
