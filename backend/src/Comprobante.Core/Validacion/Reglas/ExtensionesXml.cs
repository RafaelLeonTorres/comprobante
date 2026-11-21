using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Comprobante.Core.Calculo;
using Comprobante.Core.Xml;

namespace Comprobante.Core.Validacion.Reglas;

/// <summary>Lo mínimo para leer un CFDI como XML sin escribir el espacio de nombres ni convertir números en cada regla.</summary>
internal static class ExtensionesXml
{
    private static readonly XNamespace Cfdi = EscritorCfdi.EspacioCfdi;

    /// <summary>La línea del XML donde empieza el nodo o el atributo; nula si no hay objeto o el documento se leyó sin líneas.</summary>
    public static int? Linea(this XObject? objeto) =>
        objeto is IXmlLineInfo { } informacion && informacion.HasLineInfo() ? informacion.LineNumber : null;

    /// <summary>El valor de un atributo sin espacio de nombres, o nulo si no está.</summary>
    public static string? Valor(this XElement elemento, string atributo) => elemento.Attribute(atributo)?.Value;

    public static bool Tiene(this XElement elemento, string atributo) => elemento.Attribute(atributo) is not null;

    /// <summary>El atributo como número con punto decimal; nulo si no está o no es un número (de eso se queja el esquema).</summary>
    public static decimal? Numero(this XElement elemento, string atributo) => Numero(elemento.Valor(atributo));

    public static decimal? Numero(string? texto) =>
        texto is not null
        && decimal.TryParse(texto, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;

    /// <summary>Cuántos decimales trae escrito el atributo, ceros incluidos: así los cuenta el SAT. Cero si no está.</summary>
    public static int Decimales(this XElement elemento, string atributo) =>
        Redondeo.DecimalesEscritos(elemento.Valor(atributo) ?? string.Empty);

    /// <summary>Los hijos directos del espacio de nombres del CFDI con ese nombre.</summary>
    public static IEnumerable<XElement> Hijos(this XElement elemento, string nombre) => elemento.Elements(Cfdi + nombre);

    public static XElement? Hijo(this XElement elemento, string nombre) => elemento.Element(Cfdi + nombre);

    /// <summary>Los traslados de un nodo cfdi:Impuestos (el del concepto o el del comprobante); ninguno si el nodo no está.</summary>
    public static IEnumerable<XElement> Traslados(this XElement? impuestos) =>
        impuestos?.Hijo("Traslados")?.Hijos("Traslado") ?? [];

    /// <summary>Las retenciones de un nodo cfdi:Impuestos (el del concepto o el del comprobante); ninguna si el nodo no está.</summary>
    public static IEnumerable<XElement> Retenciones(this XElement? impuestos) =>
        impuestos?.Hijo("Retenciones")?.Hijos("Retencion") ?? [];

    /// <summary>Un número como se muestra en un mensaje: al menos dos decimales y los que necesite, sin ceros de más.</summary>
    public static string Texto(decimal valor) => Formato.Importe(valor, 2);
}
