using ExcelDataReader;

namespace Comprobante.Herramientas;

/// <summary>
/// Convierte la matriz de códigos de error del CFDI 4.0 del SAT en matriz-errores.csv. El validador toma de ahí el
/// texto oficial de cada código (CFDI40161, CFDI40153...), así los mensajes son los del SAT y no una copia a mano.
/// </summary>
internal static class ImportadorMatriz
{
    private static readonly string[] Encabezado = ["numero", "elemento", "atributo", "validacion", "codigo", "descripcion"];

    public static void Ejecutar(string rutaExcel, string carpetaSalida)
    {
        if (!File.Exists(rutaExcel))
        {
            throw new ImportacionException($"No existe el archivo {rutaExcel}.");
        }

        var filas = new List<string[]>();
        using (var flujo = File.Open(rutaExcel, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var lector = ExcelReaderFactory.CreateReader(flujo))
        {
            var fila = -1;
            while (lector.Read())
            {
                fila++;
                if (fila == 1)
                {
                    var titulo = Celda.Celdas(lector, 4);
                    if (!titulo.StartsWith("CÓDIGO", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ImportacionException($"La matriz cambió de formato: se esperaba \"CÓDIGO ERROR\" en la columna E y dice \"{titulo}\".");
                    }
                }

                if (fila < 2)
                {
                    continue;
                }

                var codigo = Celda.Celdas(lector, 4);
                if (codigo.Length == 0)
                {
                    continue;
                }

                filas.Add(
                [
                    Celda.Celdas(lector, 0), Celda.Celdas(lector, 1), Celda.Celdas(lector, 2),
                    Celda.Celdas(lector, 3), codigo, Celda.Celdas(lector, 5),
                ]);
            }
        }

        if (filas.Count == 0)
        {
            throw new ImportacionException("La matriz no trae códigos de error.");
        }

        var duplicados = filas.GroupBy(f => f[4]).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicados.Count > 0)
        {
            throw new ImportacionException($"Códigos repetidos en la matriz: {string.Join(", ", duplicados)}.");
        }

        Csv.Escribir(Path.Combine(carpetaSalida, "matriz-errores.csv"), Encabezado, filas);
        Console.WriteLine($"matriz-errores.csv: {filas.Count} códigos en {carpetaSalida}.");
    }
}
