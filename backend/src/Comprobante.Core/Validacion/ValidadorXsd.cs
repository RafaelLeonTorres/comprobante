using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Comprobante.Core.Validacion.Reglas;
using Comprobante.Core.Xml;

namespace Comprobante.Core.Validacion;

/// <summary>
/// Valida un XML contra el esquema del CFDI 4.0 y devuelve cada incumplimiento como un hallazgo. El esquema del SAT deja abiertos
/// los nodos Complemento, ComplementoConcepto y Addenda (admiten cualquier elemento), pero exige que cada elemento tenga su propio
/// esquema; la app solo trae el del Timbre Fiscal Digital. Un complemento de otro esquema no es un error del comprobante: se
/// deja sin revisar y <c>ReglasComplementos</c> lo avisa con CMP004.
/// </summary>
internal static class ValidadorXsd
{
    private static readonly XNamespace Cfdi = EscritorCfdi.EspacioCfdi;

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
                if (EsContenidoSinEsquema(remitente, args))
                {
                    return;
                }

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

    /// <summary>El esquema se queja de un elemento sin declarar que cuelga de un nodo abierto (Complemento, ComplementoConcepto o Addenda).</summary>
    private static bool EsContenidoSinEsquema(object? remitente, ValidationEventArgs args) =>
        args.Severity == XmlSeverityType.Error
        && TraductorXsd.EsElementoNoDeclarado(args.Message)
        && remitente is XElement { Parent: { } padre }
        && padre.Name.Namespace == Cfdi
        && padre.Name.LocalName is "Complemento" or "ComplementoConcepto" or "Addenda";

    private static Hallazgo Convertir(object? remitente, ValidationEventArgs args)
    {
        var objeto = remitente as XObject;
        var (mensaje, atributo) = TraductorXsd.Traducir(args.Message);

        // Cuando falla un atributo, el remitente es el atributo (o el elemento si falta): el nombre ya viene en el mensaje.
        atributo ??= (objeto as XAttribute)?.Name.LocalName;
        return new Hallazgo(
            Codigos.Esquema,
            args.Severity == XmlSeverityType.Warning ? Severidad.Advertencia : Severidad.Error,
            objeto is null ? string.Empty : RutaXml.De(objeto),
            atributo,
            mensaje,
            Linea: objeto.Linea());
    }
}
