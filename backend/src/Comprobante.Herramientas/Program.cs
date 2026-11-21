using System.Text;
using Comprobante.Herramientas;

// Los .xls (BIFF) del SAT usan páginas de códigos antiguas (Windows-1252); .NET las trae, pero hay que activarlas.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Mantenimiento de los archivos del SAT. Se corre desde la raíz del repositorio:
//   dotnet run --project backend/src/Comprobante.Herramientas -- importar-catalogos sat/origen/catCFDI_V_4_20261120.xls sat/catalogos
//   dotnet run --project backend/src/Comprobante.Herramientas -- importar-matriz sat/origen/MatrizDeErrores_CFDI_v40_20260325.xls sat/matriz
if (args.Length != 3)
{
    Console.Error.WriteLine("""
        Uso: Comprobante.Herramientas <comando> <archivo.xls> <carpeta de salida>

          importar-catalogos   Excel de catálogos del CFDI 4.0 -> un CSV por catálogo y version.json
          importar-matriz      Matriz de errores del CFDI 4.0  -> matriz-errores.csv
        """);
    return args.Length == 0 ? 0 : 1;
}

try
{
    switch (args[0])
    {
        case "importar-catalogos":
            ImportadorCatalogos.Ejecutar(args[1], args[2]);
            return 0;
        case "importar-matriz":
            ImportadorMatriz.Ejecutar(args[1], args[2]);
            return 0;
        default:
            Console.Error.WriteLine($"Comando desconocido: {args[0]}");
            return 1;
    }
}
catch (ImportacionException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 2;
}
