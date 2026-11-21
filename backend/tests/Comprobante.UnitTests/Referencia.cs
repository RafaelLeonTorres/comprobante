using System.Text;
using System.Text.Json;
using Comprobante.Core.Solicitud;

namespace Comprobante.UnitTests;

/// <summary>
/// Los archivos de la carpeta Referencia: solicitudes en JSON y, de cada una, el XML sellado y la cadena original que sacó
/// una implementación independiente (<c>generar_referencia.py</c>: Python, lxml, libxslt y OpenSSL).
/// </summary>
internal static class Referencia
{
    private static readonly string Carpeta = Path.Combine(AppContext.BaseDirectory, "Referencia");

    /// <summary>Las mismas opciones con las que la API lee el JSON (camelCase y sin distinguir mayúsculas).</summary>
    public static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public static string Leer(string archivo) => File.ReadAllText(Path.Combine(Carpeta, archivo), new UTF8Encoding(false));

    public static SolicitudCfdi Solicitud(string escenario) =>
        JsonSerializer.Deserialize<SolicitudCfdi>(Leer($"{escenario}.solicitud.json"), OpcionesJson)!;

    public static string Xml(string escenario) => Leer($"{escenario}.xml");

    public static string Cadena(string escenario) => Leer($"{escenario}.cadena.txt");

    /// <summary>Los nombres de los escenarios: uno por cada archivo <c>*.solicitud.json</c>.</summary>
    public static IReadOnlyList<string> Nombres { get; } = Directory.GetFiles(Carpeta, "*.solicitud.json")
        .Select(archivo => Path.GetFileName(archivo).Replace(".solicitud.json", string.Empty, StringComparison.Ordinal))
        .Order(StringComparer.Ordinal)
        .ToList();

    public static TheoryData<string> Escenarios()
    {
        var datos = new TheoryData<string>();
        foreach (var nombre in Nombres)
        {
            datos.Add(nombre);
        }

        return datos;
    }
}
