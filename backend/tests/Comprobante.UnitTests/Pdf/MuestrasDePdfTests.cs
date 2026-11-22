using System.Xml.Linq;
using Comprobante.Core.Representacion;
using Comprobante.Pdf;
using static Comprobante.UnitTests.Pdf.EjemplosDePdf;

namespace Comprobante.UnitTests.Pdf;

/// <summary>Un <c>Fact</c> que solo corre si se define MUESTRAS_PDF con la carpeta donde guardar los PDF de muestra.</summary>
public sealed class MuestrasFactAttribute : FactAttribute
{
    public MuestrasFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MUESTRAS_PDF")))
        {
            Skip = "Define MUESTRAS_PDF con la carpeta donde guardar los PDF de muestra para revisarlos a ojo.";
        }
    }
}

/// <summary>
/// Escribe PDF de ejemplo (los cinco escenarios de referencia con y sin timbre, con logo, sin sellos, largos) para revisar a
/// ojo la plantilla o tomar las capturas del README: <c>MUESTRAS_PDF=carpeta dotnet test --filter MuestrasDePdf</c>.
/// </summary>
[Collection(ColeccionCatalogos.Nombre)]
public class MuestrasDePdfTests(CatalogosFixture catalogos)
{
    [MuestrasFact]
    public async Task Escribe_las_muestras()
    {
        var carpeta = Environment.GetEnvironmentVariable("MUESTRAS_PDF")!;
        Directory.CreateDirectory(carpeta);
        var constructor = new ConstructorDeRepresentacion(catalogos.Catalogos);

        async Task Muestra(string nombre, XDocument documento, OpcionesDelPdf? opciones = null)
        {
            var representacion = await constructor.ConstruirAsync(documento);
            await File.WriteAllBytesAsync(Path.Combine(carpeta, nombre + ".pdf"), GeneradorPdf.Generar(representacion, opciones));
        }

        foreach (var escenario in Referencia.Nombres)
        {
            await Muestra("sin-timbre-" + escenario, SinTimbre(escenario));
            await Muestra("con-timbre-" + escenario, ConTimbre(escenario));
        }

        await Muestra("con-timbre-sin-sellos", ConTimbre(), new OpcionesDelPdf { IncluirCadenaYSellos = false });
        await Muestra("sin-timbre-sin-sellos", SinTimbre(), new OpcionesDelPdf { IncluirCadenaYSellos = false });
        await Muestra("logo-png", ConTimbre(), new OpcionesDelPdf { Logo = Logo("logo-prueba.png") });
        await Muestra("logo-jpg", SinTimbre(), new OpcionesDelPdf { Logo = Logo("logo-prueba.jpg") });
        await Muestra("logo-alto", SinTimbre(), new OpcionesDelPdf { Logo = Logo("logo-alto.png") });
        await Muestra("muchos-conceptos", ConMuchosConceptos(70));
        await Muestra("muchos-conceptos-con-timbre", ConTimbre(ConMuchosConceptos(70)));
    }
}
