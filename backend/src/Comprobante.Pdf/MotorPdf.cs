using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace Comprobante.Pdf;

/// <summary>
/// La configuración de QuestPDF, que se hace una sola vez al primer uso: la licencia Community, y las fuentes Atkinson
/// Hyperlegible que viajan en el ensamblado. Se apaga el uso de las fuentes del sistema para que el PDF salga igual en
/// cualquier equipo (y dentro del contenedor, que no trae ninguna).
/// </summary>
internal static class MotorPdf
{
    public const string FamiliaTexto = "Atkinson Hyperlegible Next";
    public const string FamiliaMono = "Atkinson Hyperlegible Mono";

    private static readonly Assembly Ensamblado = typeof(MotorPdf).Assembly;

    static MotorPdf()
    {
        // QuestPDF Community: gratis para personas, proyectos de código abierto y organizaciones chicas. Ver el README.
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
        QuestPDF.Settings.FontDiscoveryPath = null;

        // Un nombre con un carácter que la fuente no tiene (un emoji, un alfabeto no latino) sale con el cuadro de "sin
        // glifo" en vez de tirar todo el PDF.
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;

        foreach (var nombre in Ensamblado.GetManifestResourceNames().Where(n => n.StartsWith("fuentes/", StringComparison.Ordinal)))
        {
            using var flujo = Ensamblado.GetManifestResourceStream(nombre)!;
            FontManager.RegisterFontFromStream(flujo);
        }
    }

    /// <summary>Llamarlo antes de armar un documento: lo único que hace es asegurar que la configuración ya se hizo.</summary>
    public static void Iniciar()
    {
    }

    public static string LeerTexto(string recurso)
    {
        using var flujo = Ensamblado.GetManifestResourceStream(recurso)
            ?? throw new FileNotFoundException($"El recurso \"{recurso}\" no está en el ensamblado.");
        using var lector = new StreamReader(flujo);
        return lector.ReadToEnd();
    }
}
