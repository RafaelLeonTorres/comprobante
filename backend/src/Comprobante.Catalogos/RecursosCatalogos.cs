using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Comprobante.Catalogos;

/// <summary>Los CSV de sat/catalogos, que viajan como recursos incrustados del ensamblado.</summary>
internal static class RecursosCatalogos
{
    private static readonly Assembly Ensamblado = typeof(RecursosCatalogos).Assembly;

    public static Stream Abrir(string archivo) =>
        Ensamblado.GetManifestResourceStream($"catalogos/{archivo}")
        ?? throw new InvalidOperationException($"El ensamblado no trae el catálogo {archivo}.");

    public static IReadOnlyList<string> Archivos() =>
        Ensamblado.GetManifestResourceNames()
            .Where(n => n.StartsWith("catalogos/", StringComparison.Ordinal))
            .Select(n => n["catalogos/".Length..])
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>SHA-256 de todos los archivos (nombre y contenido), en hexadecimal minúscula.</summary>
    public static string Huella()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        foreach (var archivo in Archivos())
        {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(archivo + "\0"));
            using var flujo = Abrir(archivo);
            int leidos;
            while ((leidos = flujo.Read(buffer)) > 0)
            {
                hash.AppendData(buffer.AsSpan(0, leidos));
            }
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>Del version.json: el nombre del Excel del SAT y su fecha de publicación.</summary>
    public static (string Archivo, DateOnly? Publicacion) LeerVersion()
    {
        using var flujo = Abrir("version.json");
        using var documento = JsonDocument.Parse(flujo);
        var raiz = documento.RootElement;
        var archivo = raiz.GetProperty("archivo").GetString() ?? "desconocido";
        DateOnly? publicacion = raiz.TryGetProperty("publicacion", out var p)
            && p.ValueKind == JsonValueKind.String
            && DateOnly.TryParseExact(p.GetString(), "yyyy-MM-dd", out var fecha)
                ? fecha
                : null;
        return (archivo, publicacion);
    }
}
