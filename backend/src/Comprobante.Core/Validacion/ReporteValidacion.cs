namespace Comprobante.Core.Validacion;

/// <summary>Los datos del certificado de sello digital (CSD) que trae el comprobante.</summary>
public sealed record DatosCertificado(
    string NoCertificado,
    string? Rfc,
    string? Nombre,
    DateTimeOffset ValidoDesde,
    DateTimeOffset ValidoHasta);

/// <summary>Lo que devuelve el validador: los hallazgos y lo que se pudo calcular del comprobante.</summary>
/// <param name="EsValido">No hay hallazgos de severidad <see cref="Severidad.Error"/>. Las advertencias no cuentan.</param>
/// <param name="CadenaOriginal">La cadena original que resulta del XML, si se pudo calcular.</param>
/// <param name="SelloValido">Si el sello verifica contra la cadena original con la llave pública del certificado; nulo si no se pudo comprobar.</param>
/// <param name="Certificado">Los datos del certificado del comprobante, si se pudo leer.</param>
public sealed record ReporteValidacion(
    bool EsValido,
    IReadOnlyList<Hallazgo> Hallazgos,
    string? CadenaOriginal,
    bool? SelloValido,
    DatosCertificado? Certificado)
{
    public static ReporteValidacion De(
        IReadOnlyList<Hallazgo> hallazgos, string? cadenaOriginal, bool? selloValido, DatosCertificado? certificado) =>
        new(hallazgos.All(h => h.Severidad != Severidad.Error), hallazgos, cadenaOriginal, selloValido, certificado);
}
