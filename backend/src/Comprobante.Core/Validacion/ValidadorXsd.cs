using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Comprobante.Core.Xml;

namespace Comprobante.Core.Validacion;

/// <summary>Valida un XML contra el esquema del CFDI 4.0 y devuelve cada incumplimiento como un hallazgo.</summary>
internal static class ValidadorXsd
{
    /// <summary>Un XML muy roto puede dar miles de errores iguales; se muestran los primeros.</summary>
    internal const int MaximoDeHallazgos = 200;

    public static List<Hallazgo> Validar(XDocument documento)
    {
        var hallazgos = new List<Hallazgo>();
        var omitidos = 0;
        documento.Validate(
            EsquemasCfdi.Conjunto,
            (remitente, args) =>
            {
                if (hallazgos.Count >= MaximoDeHallazgos)
                {
                    omitidos++;
                    return;
                }

                hallazgos.Add(Convertir(remitente, args));
            },
            addSchemaInfo: false);

        if (omitidos > 0)
        {
            hallazgos.Add(new Hallazgo(
                Codigos.Esquema,
                Severidad.Error,
                documento.Root is { } raiz ? RutaXml.De(raiz) : string.Empty,
                null,
                $"Hay {omitidos} errores de esquema más; se muestran solo los primeros {MaximoDeHallazgos}."));
        }

        return hallazgos;
    }

    private static Hallazgo Convertir(object? remitente, ValidationEventArgs args)
    {
        var objeto = remitente as XObject;
        var (mensaje, atributo) = TraductorXsd.Traducir(args.Message);

        // Cuando falla un atributo, el remitente es el atributo (o el elemento si falta): el nombre ya viene en el mensaje.
        atributo ??= (objeto as XAttribute)?.Name.LocalName;
        int? linea = objeto is IXmlLineInfo { } info && info.HasLineInfo() ? info.LineNumber : null;
        return new Hallazgo(
            Codigos.Esquema,
            args.Severity == XmlSeverityType.Warning ? Severidad.Advertencia : Severidad.Error,
            objeto is null ? string.Empty : RutaXml.De(objeto),
            atributo,
            mensaje,
            Linea: linea);
    }
}
