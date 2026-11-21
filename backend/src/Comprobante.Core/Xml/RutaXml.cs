using System.Xml.Linq;

namespace Comprobante.Core.Xml;

/// <summary>La ruta de un nodo para los hallazgos: <c>cfdi:Comprobante/cfdi:Conceptos/cfdi:Concepto[2]</c>.</summary>
internal static class RutaXml
{
    public static string De(XObject objeto)
    {
        var elemento = objeto switch
        {
            XElement e => e,
            XAttribute a => a.Parent,
            _ => objeto.Parent,
        };
        return elemento is null ? string.Empty : De(elemento);
    }

    public static string De(XElement elemento)
    {
        var partes = new Stack<string>();
        for (var actual = elemento; actual is not null; actual = actual.Parent)
        {
            partes.Push(Nombre(actual));
        }

        return string.Join('/', partes);
    }

    private static string Nombre(XElement elemento)
    {
        var prefijo = elemento.GetPrefixOfNamespace(elemento.Name.Namespace);
        var nombre = string.IsNullOrEmpty(prefijo) ? elemento.Name.LocalName : $"{prefijo}:{elemento.Name.LocalName}";
        if (elemento.Parent is { } padre)
        {
            var hermanos = padre.Elements(elemento.Name).ToList();
            if (hermanos.Count > 1)
            {
                nombre += $"[{hermanos.IndexOf(elemento) + 1}]";
            }
        }

        return nombre;
    }
}
