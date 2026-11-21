using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace Comprobante.Herramientas;

internal enum Tipo { Texto, Relleno, Fecha, SiNo, Bandera, Decimal, Entero, Regimenes, ClaveImpuesto }

/// <param name="Nombre">Nombre de la columna en el CSV.</param>
/// <param name="Indice">Posición de la columna en la hoja del Excel (empezando en 0).</param>
/// <param name="Largo">Para <see cref="Tipo.Relleno"/>: cuántos dígitos tiene la clave.</param>
internal sealed record Columna(string Nombre, int Indice, Tipo Tipo = Tipo.Texto, int Largo = 0);

/// <summary>Cómo leer un catálogo: de qué hojas, dónde está el encabezado y qué columnas pasan al CSV.</summary>
/// <param name="Encabezado">Lo que dice la primera celda de la fila de encabezado: si cambia, el Excel cambió de formato.</param>
/// <param name="Clave">Expresión regular que cumple toda clave del catálogo; una nota al pie con texto en esa columna no pasa.</param>
/// <param name="SinClaveEsperadas">Filas con datos y sin clave que el SAT deja en la hoja a propósito: no se avisa de ellas.</param>
internal sealed record Catalogo(
    string Archivo,
    string[] Hojas,
    string Encabezado,
    int FilaEncabezado,
    int PrimeraFila,
    string Clave,
    Columna[] Columnas,
    int SinClaveEsperadas = 0);

/// <summary>
/// Convierte el Excel de catálogos del SAT (catCFDI_V_4_AAAAMMDD.xls) en un CSV por catálogo y un version.json.
/// Solo se importan los catálogos que usan el generador y el validador; los de aduanas, colonias, localidades y
/// municipios pesan decenas de miles de filas y ninguna regla los consulta.
/// </summary>
internal static class ImportadorCatalogos
{
    private static Columna[] Vigencia(int inicio, int fin) =>
        [new("inicio_vigencia", inicio, Tipo.Fecha), new("fin_vigencia", fin, Tipo.Fecha)];

    internal static readonly Catalogo[] Catalogos =
    [
        new("forma_pago.csv", ["c_FormaPago"], "c_FormaPago", 5, 6, @"^\d{2}$",
            [new("clave", 0, Tipo.Relleno, 2), new("descripcion", 1), new("bancarizado", 2, Tipo.SiNo), .. Vigencia(12, 13)]),
        new("moneda.csv", ["c_Moneda"], "c_Moneda", 4, 5, @"^[A-Z]{3}$",
            [new("clave", 0), new("descripcion", 1), new("decimales", 2, Tipo.Entero), new("porcentaje_variacion", 3, Tipo.Decimal), .. Vigencia(4, 5)]),
        // La fila de Nómina (N) ocupa dos renglones; el segundo no trae clave y sus valores máximos no se usan.
        new("tipo_de_comprobante.csv", ["c_TipoDeComprobante"], "c_TipoDeComprobante", 4, 5, @"^[A-Z]$",
            [new("clave", 0), new("descripcion", 1), new("valor_maximo", 2), .. Vigencia(4, 5)], SinClaveEsperadas: 1),
        new("exportacion.csv", ["c_Exportacion"], "c_Exportacion", 4, 5, @"^\d{2}$",
            [new("clave", 0, Tipo.Relleno, 2), new("descripcion", 1), .. Vigencia(2, 3)]),
        new("metodo_pago.csv", ["c_MetodoPago"], "c_MetodoPago", 5, 6, @"^[A-Z]{3}$",
            [new("clave", 0), new("descripcion", 1), .. Vigencia(2, 3)]),
        new("codigo_postal.csv", ["c_CodigoPostal_Parte_1", "c_CodigoPostal_Parte_2"], "c_CodigoPostal", 5, 7, @"^\d{5}$",
            [
                new("clave", 0, Tipo.Relleno, 5), new("estado", 1), new("municipio", 2, Tipo.Relleno, 3),
                new("localidad", 3, Tipo.Relleno, 2), new("estimulo_franja_fronteriza", 4, Tipo.Bandera), .. Vigencia(5, 6),
                new("huso_horario", 7), new("mes_inicio_verano", 8), new("dia_inicio_verano", 9), new("hora_inicio_verano", 10),
                new("diferencia_verano", 11, Tipo.Entero), new("mes_inicio_invierno", 12), new("dia_inicio_invierno", 13),
                new("hora_inicio_invierno", 14), new("diferencia_invierno", 15, Tipo.Entero),
            ]),
        new("periodicidad.csv", ["c_Periodicidad"], "c_Periodicidad", 4, 5, @"^\d{2}$",
            [new("clave", 0, Tipo.Relleno, 2), new("descripcion", 1), .. Vigencia(2, 3)]),
        new("meses.csv", ["c_Meses"], "c_Meses", 4, 5, @"^\d{2}$",
            [new("clave", 0, Tipo.Relleno, 2), new("descripcion", 1), .. Vigencia(2, 3)]),
        new("tipo_relacion.csv", ["c_TipoRelacion"], "c_TipoRelacion", 4, 5, @"^\d{2}$",
            [new("clave", 0, Tipo.Relleno, 2), new("descripcion", 1), .. Vigencia(2, 3)]),
        new("regimen_fiscal.csv", ["c_RegimenFiscal"], "c_RegimenFiscal", 5, 6, @"^\d{3}$",
            [new("clave", 0, Tipo.Relleno, 3), new("descripcion", 1), new("fisica", 2, Tipo.SiNo), new("moral", 3, Tipo.SiNo), .. Vigencia(4, 5)]),
        new("pais.csv", ["c_Pais"], "c_Pais", 4, 5, @"^[A-Z]{3}$",
            [
                new("clave", 0), new("descripcion", 1), new("formato_codigo_postal", 2),
                new("formato_registro_identidad_tributaria", 3), new("validacion_registro_identidad_tributaria", 4),
                new("agrupaciones", 5),
            ]),
        new("uso_cfdi.csv", ["c_UsoCFDI"], "c_UsoCFDI", 4, 6, @"^[A-Z]{1,2}\d{2}$",
            [
                new("clave", 0), new("descripcion", 1), new("fisica", 2, Tipo.SiNo), new("moral", 3, Tipo.SiNo), .. Vigencia(4, 5),
                new("regimenes_receptor", 6, Tipo.Regimenes),
            ]),
        new("clave_prod_serv.csv", ["c_ClaveProdServ"], "c_ClaveProdServ", 4, 5, @"^\d{8}$",
            [
                new("clave", 0, Tipo.Relleno, 8), new("descripcion", 1), new("incluir_iva", 2), new("incluir_ieps", 3),
                new("complemento", 4), .. Vigencia(5, 6), new("estimulo_franja_fronteriza", 7, Tipo.Bandera),
                new("palabras_similares", 8),
            ]),
        new("clave_unidad.csv", ["c_ClaveUnidad"], "c_ClaveUnidad", 5, 6, @"^[A-Za-z0-9]{1,3}$",
            [new("clave", 0), new("nombre", 1), new("descripcion", 2), .. Vigencia(4, 5), new("simbolo", 6)]),
        new("objeto_imp.csv", ["c_ObjetoImp"], "c_ObjetoImp", 4, 5, @"^\d{2}$",
            [new("clave", 0, Tipo.Relleno, 2), new("descripcion", 1), .. Vigencia(2, 3)]),
        new("impuesto.csv", ["c_Impuesto"], "c_Impuesto", 4, 5, @"^\d{3}$",
            [
                new("clave", 0, Tipo.Relleno, 3), new("descripcion", 1), new("retencion", 2, Tipo.SiNo), new("traslado", 3, Tipo.SiNo),
                new("local_o_federal", 4), .. Vigencia(5, 6),
            ]),
        new("tipo_factor.csv", ["c_TipoFactor"], "c_TipoFactor", 4, 5, @"^(Tasa|Cuota|Exento)$",
            [new("clave", 0), .. Vigencia(1, 2)]),
        new("tasa_o_cuota.csv", ["c_TasaOCuota"], "Rango o Fijo", 4, 6, @"^(Fijo|Rango)$",
            [
                new("rango_o_fijo", 0), new("valor_minimo", 1, Tipo.Decimal), new("valor_maximo", 2, Tipo.Decimal),
                new("impuesto", 3, Tipo.ClaveImpuesto), new("factor", 4), new("traslado", 5, Tipo.SiNo),
                new("retencion", 6, Tipo.SiNo), .. Vigencia(7, 8),
            ]),
        new("estado.csv", ["c_Estado"], "c_Estado", 4, 5, @"^[A-Z]{2,3}$",
            [new("clave", 0), new("pais", 1), new("nombre", 2), .. Vigencia(3, 4)]),
    ];

    private sealed record VersionHoja(string Nombre, string? Version, string? Revision, string? Publicacion, int Filas);

    public static void Ejecutar(string rutaExcel, string carpetaSalida)
    {
        if (!File.Exists(rutaExcel))
        {
            throw new ImportacionException($"No existe el archivo {rutaExcel}.");
        }

        Console.WriteLine($"Leyendo {rutaExcel} ...");
        var filas = Catalogos.ToDictionary(c => c.Archivo, _ => new List<string[]>());
        var versiones = new Dictionary<string, VersionHoja>(StringComparer.OrdinalIgnoreCase);
        var porHoja = Catalogos
            .SelectMany(c => c.Hojas.Select(h => (Hoja: h, Catalogo: c)))
            .ToDictionary(x => x.Hoja, x => x.Catalogo, StringComparer.OrdinalIgnoreCase);

        using (var flujo = File.Open(rutaExcel, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var lector = ExcelReaderFactory.CreateReader(flujo))
        {
            do
            {
                if (!porHoja.TryGetValue(lector.Name, out var catalogo))
                {
                    continue;
                }

                LeerHoja(lector, catalogo, filas[catalogo.Archivo], versiones);
            }
            while (lector.NextResult());
        }

        foreach (var catalogo in Catalogos)
        {
            foreach (var hoja in catalogo.Hojas.Where(h => !versiones.ContainsKey(h)))
            {
                throw new ImportacionException($"El Excel no tiene la hoja {hoja}.");
            }

            var datos = filas[catalogo.Archivo];
            if (datos.Count == 0)
            {
                throw new ImportacionException($"La hoja {string.Join(" y ", catalogo.Hojas)} no tiene filas de datos.");
            }

            Csv.Escribir(Path.Combine(carpetaSalida, catalogo.Archivo), catalogo.Columnas.Select(c => c.Nombre).ToArray(), datos);
            Console.WriteLine($"  {catalogo.Archivo,-26} {datos.Count,7:N0} filas");
        }

        EscribirVersion(rutaExcel, carpetaSalida, versiones, filas);
        Console.WriteLine($"Listo: {Catalogos.Length} catálogos en {carpetaSalida}.");
    }

    private static void LeerHoja(
        IExcelDataReader lector, Catalogo catalogo, List<string[]> salida, Dictionary<string, VersionHoja> versiones)
    {
        var fila = -1;
        var buscandoVersion = false;
        string? version = null, revision = null, publicacion = null;
        var contadas = 0;
        var sinClave = 0;
        while (lector.Read())
        {
            fila++;
            if (fila < catalogo.PrimeraFila)
            {
                if (buscandoVersion)
                {
                    buscandoVersion = false;
                    version = Celda.Celdas(lector, 1);
                    revision = Celda.Celdas(lector, 2);
                    var fecha = Celda.Fecha(lector.GetValue(3));
                    publicacion = fecha.Length > 0 ? fecha : null;
                }
                else if (Celda.Celdas(lector, 0).StartsWith("Versión CFDI", StringComparison.OrdinalIgnoreCase))
                {
                    buscandoVersion = true;
                }

                if (fila == catalogo.FilaEncabezado)
                {
                    var encontrado = Celda.Celdas(lector, 0);
                    if (!string.Equals(encontrado, catalogo.Encabezado, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ImportacionException(
                            $"El Excel cambió de formato: en la hoja {lector.Name} se esperaba \"{catalogo.Encabezado}\" en la fila {fila + 1} y dice \"{encontrado}\".");
                    }
                }

                continue;
            }

            // Sin clave no hay dato: son filas en blanco o notas al final de la hoja.
            var clave = Celda.Celdas(lector, catalogo.Columnas[0].Indice);
            if (clave.Length == 0)
            {
                var contenido = string.Join(" | ", Enumerable.Range(0, lector.FieldCount).Select(i => Celda.Celdas(lector, i)).Where(t => t.Length > 0));
                if (contenido.Length > 0 && ++sinClave > catalogo.SinClaveEsperadas)
                {
                    Console.WriteLine($"  aviso: {lector.Name}, fila {fila + 1}, trae datos pero no clave y se ignora: {contenido[..Math.Min(contenido.Length, 90)]}");
                }

                continue;
            }

            // Una clave con formato raro es una nota al pie ("Continúa en c_CodigoPostal_2") si solo esa celda trae texto;
            // si la fila trae más datos, el catálogo cambió y es mejor parar que cargarlo mal.
            var normalizada = Convertir(lector, catalogo.Columnas[0]);
            if (!Regex.IsMatch(normalizada, catalogo.Clave))
            {
                var otrasCeldas = Enumerable.Range(0, lector.FieldCount).Count(i => Celda.Celdas(lector, i).Length > 0);
                if (otrasCeldas > 1)
                {
                    throw new ImportacionException(
                        $"En la hoja {lector.Name}, fila {fila + 1}, la clave \"{normalizada}\" no cumple {catalogo.Clave}. ¿El SAT cambió el catálogo?");
                }

                Console.WriteLine($"  nota ignorada en {lector.Name}, fila {fila + 1}: {normalizada}");
                continue;
            }

            salida.Add(catalogo.Columnas.Select(c => Convertir(lector, c)).ToArray());
            contadas++;
        }

        versiones[lector.Name] = new VersionHoja(lector.Name, version, revision, publicacion, contadas);
    }

    private static string Convertir(IExcelDataReader lector, Columna columna)
    {
        var valor = columna.Indice < lector.FieldCount ? lector.GetValue(columna.Indice) : null;
        return columna.Tipo switch
        {
            Tipo.Relleno => Celda.Relleno(valor, columna.Largo),
            Tipo.Fecha => Celda.Fecha(valor),
            Tipo.SiNo => Celda.SiNo(valor),
            Tipo.Bandera => Celda.Bandera(valor),
            Tipo.Decimal => Celda.Decimal(valor),
            Tipo.Entero => Celda.Entero(valor),
            Tipo.Regimenes => Celda.Regimenes(valor),
            Tipo.ClaveImpuesto => Celda.ClaveImpuesto(valor),
            _ => Celda.Texto(valor),
        };
    }

    private static void EscribirVersion(
        string rutaExcel, string carpetaSalida, Dictionary<string, VersionHoja> versiones, Dictionary<string, List<string[]>> filas)
    {
        var nombre = Path.GetFileName(rutaExcel);
        var coincidencia = Regex.Match(nombre, @"_(\d{4})(\d{2})(\d{2})\.xls", RegexOptions.IgnoreCase);
        // Los Excel antiguos traen la fecha como DDMMAAAA: catCFDI_V_4_27072022.xls
        var publicacion = coincidencia.Success
            ? $"{coincidencia.Groups[1].Value}-{coincidencia.Groups[2].Value}-{coincidencia.Groups[3].Value}"
            : null;
        string hash;
        using (var flujo = File.OpenRead(rutaExcel))
        {
            hash = Convert.ToHexStringLower(SHA256.HashData(flujo));
        }

        var documento = new
        {
            archivo = nombre,
            publicacion,
            sha256 = hash,
            catalogos = Catalogos.Select(c => new
            {
                archivo = c.Archivo,
                hojas = c.Hojas,
                filas = filas[c.Archivo].Count,
                version = versiones[c.Hojas[0]].Version,
                revision = versiones[c.Hojas[0]].Revision,
                publicacion = c.Hojas.Select(h => versiones[h].Publicacion).Where(p => p is not null).OrderDescending().FirstOrDefault(),
            }),
        };
        var opciones = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        File.WriteAllText(Path.Combine(carpetaSalida, "version.json"), JsonSerializer.Serialize(documento, opciones) + "\n", new System.Text.UTF8Encoding(false));
    }
}
