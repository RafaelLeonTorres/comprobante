using System.Text.RegularExpressions;
using System.Xml;

namespace Comprobante.Core.Validacion;

/// <summary>
/// Pasa a español el motivo por el que .NET no pudo leer un XML (sus mensajes salen siempre en inglés). Reconoce los errores
/// más comunes de un XML mal escrito; el que no reconoce lo explica en términos generales y conserva el texto original.
/// </summary>
internal static partial class TraductorXml
{
    public static Hallazgo Traducir(XmlException ex) =>
        new(
            Codigos.XmlNoLegible,
            Severidad.Error,
            string.Empty,
            null,
            $"El XML no se pudo leer: {Motivo(ex.Message)}",
            Linea: ex.LineNumber > 0 ? ex.LineNumber : null);

    internal static string Motivo(string original)
    {
        // La posición ya va aparte en el hallazgo; el mensaje de .NET la repite al final.
        var texto = Posicion().Replace(original, string.Empty).Trim();

        if (texto.Contains("DTD is prohibited", StringComparison.Ordinal))
        {
            return "trae una declaración DOCTYPE (DTD). Por seguridad no se aceptan: quítala del XML para poder validarlo.";
        }

        if (texto.Contains("MaxCharactersInDocument", StringComparison.Ordinal))
        {
            return "el XML es más grande de lo permitido (2 MB).";
        }

        if (texto.Contains("Data at the root level is invalid", StringComparison.Ordinal))
        {
            // .NET dice lo mismo para lo que sobra antes del primer elemento y para lo que sobra después de que se cierra el raíz.
            return "hay datos inválidos fuera del elemento raíz: texto o caracteres extraños antes de <?xml ...?> o de <cfdi:Comprobante>, o después de </cfdi:Comprobante>.";
        }

        if (texto.Contains("Root element is missing", StringComparison.Ordinal))
        {
            return "el XML está vacío o no tiene un elemento raíz.";
        }

        if (texto.Contains("multiple root elements", StringComparison.Ordinal))
        {
            return "hay más de un elemento raíz: un CFDI tiene un solo cfdi:Comprobante.";
        }

        if (EtiquetaDistinta().Match(texto) is { Success: true } distinta)
        {
            return $"la etiqueta de cierre </{distinta.Groups["cierre"].Value}> no corresponde con la de apertura <{distinta.Groups["apertura"].Value}> de la línea {distinta.Groups["linea"].Value}.";
        }

        if (SinCerrar().Match(texto) is { Success: true } sinCerrar)
        {
            return $"el XML termina antes de tiempo: faltan por cerrar los elementos {sinCerrar.Groups["elementos"].Value}.";
        }

        if (texto.Contains("Unexpected end of file", StringComparison.Ordinal))
        {
            return "el XML termina antes de tiempo: está incompleto.";
        }

        if (SinEspacio().Match(texto) is { Success: true } pegado)
        {
            return $"falta un espacio antes de «{pegado.Groups["hallado"].Value}»: los atributos de un elemento van separados por espacios.";
        }

        if (TokenInesperado().Match(texto) is { Success: true } token)
        {
            var hallado = token.Groups["hallado"].Value;
            return token.Groups["esperado"].Value == "\""
                ? $"el valor de un atributo debe ir entre comillas (se encontró «{hallado}» donde debía abrirse la comilla)."
                : $"se encontró «{hallado}» donde se esperaba «{token.Groups["esperado"].Value}».";
        }

        if (texto.Contains("An error occurred while parsing EntityName", StringComparison.Ordinal))
        {
            return "hay un & suelto o mal escrito: en XML el & se escribe &amp; (o es el inicio de una entidad completa como &lt;).";
        }

        if (PrefijoNoDeclarado().Match(texto) is { Success: true } prefijo)
        {
            return $"el prefijo «{prefijo.Groups["prefijo"].Value}:» no está declarado: falta el atributo xmlns:{prefijo.Groups["prefijo"].Value}=\"...\" que lo define.";
        }

        if (EntidadNoDeclarada().Match(texto) is { Success: true } entidad)
        {
            return $"hay una referencia a la entidad &{entidad.Groups["nombre"].Value}; que no está declarada. En XML solo se permiten &amp; &lt; &gt; &quot; y &apos; (un & suelto se escribe &amp;).";
        }

        if (AtributoRepetido().Match(texto) is { Success: true } repetido)
        {
            return $"el atributo {repetido.Groups["nombre"].Value} está repetido en el mismo elemento.";
        }

        if (CaracterEnAtributo().Match(texto) is { Success: true } enAtributo)
        {
            return $"el carácter «{enAtributo.Groups["caracter"].Value}» (código {enAtributo.Groups["codigo"].Value}) no puede ir en el valor de un atributo; si es < o &, escríbelo como &lt; o &amp;.";
        }

        if (CaracterInvalido().Match(texto) is { Success: true } invalido)
        {
            return $"hay un carácter que no es válido en XML (código {invalido.Groups["codigo"].Value}).";
        }

        if (NombreInvalido().Match(texto) is { Success: true } nombre)
        {
            return $"el carácter «{nombre.Groups["caracter"].Value}» no puede ir en un nombre de elemento o de atributo.";
        }

        if (texto.Contains("unclosed literal string", StringComparison.Ordinal))
        {
            return "falta una comilla de cierre en el valor de un atributo.";
        }

        if (texto.Contains("XML declaration", StringComparison.Ordinal))
        {
            return "la declaración <?xml ...?> debe ser lo primero del documento (sin espacios ni líneas antes) y estar bien escrita.";
        }

        if (texto.Contains("An XML comment cannot contain", StringComparison.Ordinal))
        {
            return "un comentario XML no puede contener dos guiones seguidos (--).";
        }

        if (texto.Contains("Unexpected end tag", StringComparison.Ordinal))
        {
            return "hay una etiqueta de cierre que no tiene su etiqueta de apertura.";
        }

        return $"no está bien formado ({texto}).";
    }

    [GeneratedRegex(@"\s*Line \d+, position \d+\.?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex Posicion();

    [GeneratedRegex(@"The '(?<apertura>.+?)' start tag on line (?<linea>\d+) position \d+ does not match the end tag of '(?<cierre>.+?)'", RegexOptions.CultureInvariant)]
    private static partial Regex EtiquetaDistinta();

    [GeneratedRegex(@"The following elements are not closed: (?<elementos>.+?)\.?$", RegexOptions.CultureInvariant)]
    private static partial Regex SinCerrar();

    [GeneratedRegex(@"'(?<hallado>[^']*)' is an unexpected token\. The expected token is '(?<esperado>[^']*)'", RegexOptions.CultureInvariant)]
    private static partial Regex TokenInesperado();

    [GeneratedRegex(@"'(?<hallado>[^']*)' is an unexpected token\. Expecting white ?space", RegexOptions.CultureInvariant)]
    private static partial Regex SinEspacio();

    [GeneratedRegex(@"'(?<prefijo>[^']*)' is an undeclared prefix", RegexOptions.CultureInvariant)]
    private static partial Regex PrefijoNoDeclarado();

    [GeneratedRegex(@"Reference to undeclared entity '(?<nombre>[^']*)'", RegexOptions.CultureInvariant)]
    private static partial Regex EntidadNoDeclarada();

    [GeneratedRegex(@"'(?<nombre>[^']*)' is a duplicate attribute name", RegexOptions.CultureInvariant)]
    private static partial Regex AtributoRepetido();

    [GeneratedRegex(@"'(?<caracter>[^']*)', hexadecimal value (?<codigo>0x[0-9A-Fa-f]+), is an invalid attribute character", RegexOptions.CultureInvariant)]
    private static partial Regex CaracterEnAtributo();

    [GeneratedRegex(@"hexadecimal value (?<codigo>0x[0-9A-Fa-f]+), is an invalid character", RegexOptions.CultureInvariant)]
    private static partial Regex CaracterInvalido();

    [GeneratedRegex(@"(?:Name cannot begin with the|The) '(?<caracter>[^']*)' character", RegexOptions.CultureInvariant)]
    private static partial Regex NombreInvalido();
}
