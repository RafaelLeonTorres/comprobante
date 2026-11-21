using System.Xml;

namespace Comprobante.Core.Xml;

/// <summary>
/// Entrega los esquemas y los XSLT del SAT desde el ensamblado cuando el XSD o el XSLT los piden por su dirección
/// (<c>http://www.sat.gob.mx/sitio_internet/cfd/catalogos/catCFDI.xsd</c>). Es lo único que se resuelve: cualquier otra
/// dirección falla, así que ni los esquemas ni los XSLT pueden hacer que la app lea un archivo local o pida algo a la red.
/// </summary>
internal sealed class ResolverSat : XmlResolver
{
    public const string DireccionBase = "http://www.sat.gob.mx/sitio_internet/";

    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        var nombre = NombreDelRecurso(absoluteUri)
            ?? throw new XmlException($"Solo se pueden resolver los archivos del SAT, no \"{absoluteUri}\".");
        return RecursosSat.Abrir(nombre);
    }

    /// <summary>El recurso que corresponde a una dirección del SAT, o nulo si no es de los archivos que se incluyen.</summary>
    internal static string? NombreDelRecurso(Uri direccion)
    {
        if (!direccion.IsAbsoluteUri
            || !string.Equals(direccion.Host, "www.sat.gob.mx", StringComparison.OrdinalIgnoreCase)
            || direccion.Scheme is not ("http" or "https"))
        {
            return null;
        }

        // Los archivos se identifican por su nombre: en el portal del SAT cambian de carpeta, pero no de nombre.
        var archivo = Path.GetFileName(direccion.AbsolutePath);
        var carpeta = Path.GetExtension(archivo).ToLowerInvariant() switch
        {
            ".xsd" => "esquemas",
            ".xslt" => "xslt",
            _ => null,
        };
        if (carpeta is null)
        {
            return null;
        }

        var nombre = $"sat/{carpeta}/{archivo}";
        return RecursosSat.Existe(nombre) ? nombre : null;
    }
}
