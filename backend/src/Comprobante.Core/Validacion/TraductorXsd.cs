using System.Text.RegularExpressions;

namespace Comprobante.Core.Validacion;

/// <summary>
/// Pasa a español los mensajes más comunes del validador de esquemas de .NET (que siempre salen en inglés). Lo que no
/// reconoce lo deja tal cual, para no perder información.
/// </summary>
internal static partial class TraductorXsd
{
    public static (string Mensaje, string? Atributo) Traducir(string original)
    {
        var texto = original.Trim();

        if (AtributoFaltante().Match(texto) is { Success: true } faltante)
        {
            var atributo = faltante.Groups["a"].Value;
            return ($"Falta el atributo requerido {atributo}.", atributo);
        }

        if (ValorInvalido().Match(texto) is { Success: true } invalido)
        {
            var atributo = invalido.Groups["a"].Value;
            var valor = invalido.Groups["v"].Value;
            var tipo = NombreCorto(invalido.Groups["t"].Value);
            var motivo = Restriccion(invalido.Groups["r"].Value);
            var valorTexto = valor.Length == 0 ? "vacío" : $"\"{Recortar(valor)}\"";
            return ($"El valor {valorTexto} del atributo {atributo} {motivo} (tipo {tipo}).", atributo);
        }

        if (AtributoNoDeclarado().Match(texto) is { Success: true } sobrante)
        {
            var atributo = sobrante.Groups["a"].Value;
            return ($"El atributo {atributo} no existe en el esquema del CFDI 4.0.", atributo);
        }

        if (ElementoNoDeclarado().Match(texto) is { Success: true } desconocido)
        {
            return ($"El elemento {desconocido.Groups["e"].Value} no existe en el esquema del CFDI 4.0.", null);
        }

        if (HijoNoValido().Match(texto) is { Success: true } hijo)
        {
            var esperados = Esperados(hijo.Groups["l"].Value);
            var sufijo = esperados.Length == 0 ? string.Empty : $" Se esperaba: {esperados}.";
            return (
                $"El elemento {hijo.Groups["h"].Value} no puede ir en ese lugar dentro de {hijo.Groups["e"].Value}: va en otro orden o está de más.{sufijo}",
                null);
        }

        if (ContenidoIncompleto().Match(texto) is { Success: true } incompleto)
        {
            var esperados = Esperados(incompleto.Groups["l"].Value);
            var sufijo = esperados.Length == 0 ? string.Empty : $" Falta: {esperados}.";
            return ($"El elemento {incompleto.Groups["e"].Value} está incompleto.{sufijo}", null);
        }

        return (texto, null);
    }

    private static string Restriccion(string restriccion) => restriccion switch
    {
        _ when restriccion.Contains("Pattern", StringComparison.Ordinal) => "no tiene el formato que pide el esquema",
        _ when restriccion.Contains("Enumeration", StringComparison.Ordinal) => "no es uno de los valores permitidos",
        _ when restriccion.Contains("MaxLength", StringComparison.Ordinal) => "es más largo de lo permitido",
        _ when restriccion.Contains("MinLength", StringComparison.Ordinal) => "es más corto de lo permitido",
        _ when restriccion.Contains("MinInclusive", StringComparison.Ordinal) => "es menor que el mínimo permitido",
        _ when restriccion.Contains("MaxInclusive", StringComparison.Ordinal) => "es mayor que el máximo permitido",
        _ when restriccion.Contains("TotalDigits", StringComparison.Ordinal)
            || restriccion.Contains("FractionDigits", StringComparison.Ordinal) => "tiene más dígitos o decimales de los permitidos",
        _ when restriccion.Contains("not a valid", StringComparison.Ordinal) => "no es un valor válido para su tipo",
        _ => $"no es válido ({restriccion.TrimEnd('.')})",
    };

    private static string NombreCorto(string tipo)
    {
        var corte = tipo.LastIndexOfAny([':', '}', '/']);
        return corte >= 0 ? tipo[(corte + 1)..] : tipo;
    }

    private static string Recortar(string valor) => valor.Length <= 40 ? valor : valor[..40] + "…";

    /// <summary>De "'cfdi:Emisor' in namespace '...', 'cfdi:Receptor' in namespace '...'" a "cfdi:Emisor, cfdi:Receptor".</summary>
    private static string Esperados(string lista) =>
        string.Join(", ", NombreEnLista().Matches(lista).Select(m => m.Groups["n"].Value).Distinct());

    [GeneratedRegex(@"^The required attribute '(?<a>[^']+)' is missing", RegexOptions.CultureInvariant)]
    private static partial Regex AtributoFaltante();

    [GeneratedRegex(
        @"^The '(?<a>[^']+)' attribute is invalid - The value '(?<v>.*)' is invalid according to its datatype '(?<t>[^']+)' - (?<r>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex ValorInvalido();

    [GeneratedRegex(@"^The '(?<a>[^']+)' attribute is not declared", RegexOptions.CultureInvariant)]
    private static partial Regex AtributoNoDeclarado();

    [GeneratedRegex(@"^The '(?<e>[^']+)' element is not declared", RegexOptions.CultureInvariant)]
    private static partial Regex ElementoNoDeclarado();

    [GeneratedRegex(
        @"^The element '(?<e>[^']+)' in namespace '[^']*' has invalid child element '(?<h>[^']+)' in namespace '[^']*'\.(?: List of possible elements expected: (?<l>.+?)\.?)?$",
        RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex HijoNoValido();

    [GeneratedRegex(
        @"^The element '(?<e>[^']+)' in namespace '[^']*' has incomplete content\.(?: List of possible elements expected: (?<l>.+?)\.?)?$",
        RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex ContenidoIncompleto();

    [GeneratedRegex(@"'(?<n>[^']+)' in namespace", RegexOptions.CultureInvariant)]
    private static partial Regex NombreEnLista();
}
