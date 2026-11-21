using System.Xml;
using System.Xml.Linq;

namespace Comprobante.Core.Xml;

/// <summary>
/// Lee el XML que llega de afuera sin dejarse engañar por él: no admite DTD (con lo que no hay entidades externas ni
/// "bombas" de entidades como billion laughs), no resuelve ninguna dirección y limita lo que lee.
/// </summary>
public static class LectorXmlSeguro
{
    /// <summary>Lo que se acepta de un XML (2 MB), el mismo límite que pone nginx.</summary>
    public const int TamanoMaximoPorOmision = 2 * 1024 * 1024;

    public static XmlReaderSettings Configuracion(long maximoCaracteres = TamanoMaximoPorOmision) => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersFromEntities = 0,
        MaxCharactersInDocument = maximoCaracteres,
        IgnoreProcessingInstructions = true,
    };

    /// <summary>Carga el XML. Lanza <see cref="XmlException"/> si no está bien formado, trae un DTD o es demasiado grande.</summary>
    public static XDocument Cargar(string xml, long maximoCaracteres = TamanoMaximoPorOmision)
    {
        using var texto = new StringReader(xml);
        using var lector = XmlReader.Create(texto, Configuracion(maximoCaracteres));
        return XDocument.Load(lector, LoadOptions.SetLineInfo);
    }
}
