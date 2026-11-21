using System.Text.Json.Serialization;

namespace Comprobante.Core.Validacion;

/// <summary>En JSON va como texto: <c>error</c> o <c>advertencia</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Severidad>))]
public enum Severidad
{
    /// <summary>Un PAC rechazaría el comprobante.</summary>
    [JsonStringEnumMemberName("error")]
    Error,

    /// <summary>No lo rechazaría, pero conviene revisarlo.</summary>
    [JsonStringEnumMemberName("advertencia")]
    Advertencia,
}

/// <summary>
/// Algo que no cumple en un XML. <see cref="Codigo"/> es el de la matriz de errores del SAT (<c>CFDI40161</c>) o, cuando el
/// SAT no tiene uno (errores del esquema y advertencias), uno propio con prefijo <c>CMP</c>.
/// </summary>
/// <param name="Nodo">Ruta del nodo, con el prefijo y la posición cuando se repite: <c>cfdi:Comprobante/cfdi:Conceptos/cfdi:Concepto[2]</c>.</param>
/// <param name="Atributo">El atributo que falla, si es uno solo.</param>
/// <param name="Encontrado">El valor que trae el XML, si ayuda a corregirlo.</param>
/// <param name="Esperado">El valor que debería traer, si se puede calcular (por ejemplo, el Total).</param>
/// <param name="Linea">Línea del XML donde está el nodo, si se conoce.</param>
public sealed record Hallazgo(
    string Codigo,
    Severidad Severidad,
    string Nodo,
    string? Atributo,
    string Mensaje,
    string? Encontrado = null,
    string? Esperado = null,
    int? Linea = null);

/// <summary>Los códigos propios de Comprobante, para lo que la matriz de errores del SAT no cubre.</summary>
public static class Codigos
{
    /// <summary>El XML no es un documento XML bien formado, o no se pudo leer.</summary>
    public const string XmlNoLegible = "CMP001";

    /// <summary>El XML no es un CFDI 4.0 (otro espacio de nombres, otro nodo raíz u otra versión).</summary>
    public const string NoEsCfdi40 = "CMP002";

    /// <summary>El XML no cumple el esquema (XSD) del CFDI 4.0.</summary>
    public const string Esquema = "CMP003";

    /// <summary>Un complemento que Comprobante no revisa: se validó el CFDI y se dejó el complemento sin revisar.</summary>
    public const string ComplementoNoRevisado = "CMP004";

    /// <summary>No se pudo calcular la cadena original con el XSLT del SAT, así que tampoco se puede revisar el sello.</summary>
    public const string CadenaOriginalNoCalculable = "CMP005";

    /// <summary>Hubo más hallazgos de las reglas del SAT de los que se muestran: se guardan los primeros y se cuenta el resto.</summary>
    public const string ReglasOmitidas = "CMP006";

    /// <summary>La Fecha del comprobante está en el futuro según la zona horaria del lugar de expedición.</summary>
    public const string FechaEnElFuturo = "CMP101";
}
