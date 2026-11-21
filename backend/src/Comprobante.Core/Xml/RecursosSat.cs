using System.Reflection;

namespace Comprobante.Core.Xml;

/// <summary>Los archivos del SAT que viajan dentro del ensamblado (sat/esquemas, sat/xslt y sat/csd-prueba).</summary>
internal static class RecursosSat
{
    private static readonly Assembly Ensamblado = typeof(RecursosSat).Assembly;

    private static readonly HashSet<string> Nombres = new(Ensamblado.GetManifestResourceNames(), StringComparer.Ordinal);

    public static bool Existe(string nombre) => Nombres.Contains(nombre);

    public static Stream Abrir(string nombre) =>
        Ensamblado.GetManifestResourceStream(nombre)
        ?? throw new FileNotFoundException($"El archivo del SAT \"{nombre}\" no está en el ensamblado.");

    public static byte[] Bytes(string nombre)
    {
        using var flujo = Abrir(nombre);
        using var memoria = new MemoryStream();
        flujo.CopyTo(memoria);
        return memoria.ToArray();
    }
}
