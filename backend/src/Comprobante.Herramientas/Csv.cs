using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace Comprobante.Herramientas;

/// <summary>El Excel no tiene el formato que se esperaba: el SAT cambió el archivo y hay que revisar el importador.</summary>
public sealed class ImportacionException(string mensaje) : Exception(mensaje);

/// <summary>Escritura de CSV (RFC 4180, UTF-8 sin BOM y saltos de línea LF) para que los cambios se vean bien en git.</summary>
internal static class Csv
{
    public static string Campo(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return "";
        }

        return valor.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{valor.Replace("\"", "\"\"")}\"" : valor;
    }

    public static void Escribir(string ruta, IReadOnlyList<string> encabezado, IEnumerable<string[]> filas)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ruta))!);
        using var escritor = new StreamWriter(ruta, false, new UTF8Encoding(false)) { NewLine = "\n" };
        escritor.Write(string.Join(',', encabezado.Select(Campo)));
        escritor.Write('\n');
        foreach (var fila in filas)
        {
            escritor.Write(string.Join(',', fila.Select(Campo)));
            escritor.Write('\n');
        }
    }
}

/// <summary>Lectura tolerante de las celdas del Excel, que mezcla números, fechas y texto en una misma columna.</summary>
internal static class Celda
{
    public static string Texto(object? valor) => valor switch
    {
        null => "",
        string texto => texto.Trim().Replace("\r", "").Replace("\n", " "),
        double numero => Numero(numero),
        DateTime fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool booleano => booleano ? "true" : "false",
        _ => Convert.ToString(valor, CultureInfo.InvariantCulture)?.Trim() ?? "",
    };

    /// <summary>
    /// Un entero se escribe sin decimales ("18", no "18.0"). Un decimal se escribe con su representación más corta
    /// ("0.16"): convertir el double directamente a decimal arrastraría el error binario (0.16000000000000000333...).
    /// </summary>
    private static string Numero(double numero)
    {
        if (numero == Math.Floor(numero) && Math.Abs(numero) < 1e15)
        {
            return ((long)numero).ToString(CultureInfo.InvariantCulture);
        }

        var corto = numero.ToString("R", CultureInfo.InvariantCulture);
        return decimal.TryParse(corto, NumberStyles.Float, CultureInfo.InvariantCulture, out var exacto)
            ? exacto.ToString(CultureInfo.InvariantCulture)
            : corto;
    }

    /// <summary>Claves numéricas que Excel guarda sin los ceros de la izquierda ("1" en lugar de "01").</summary>
    public static string Relleno(object? valor, int largo)
    {
        var texto = Texto(valor);
        return texto.Length is > 0 && texto.Length < largo && texto.All(char.IsAsciiDigit)
            ? texto.PadLeft(largo, '0')
            : texto;
    }

    public static string Fecha(object? valor)
    {
        switch (valor)
        {
            case DateTime fecha:
                return fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            case string texto when texto.Trim().Length > 0:
                var formatos = new[] { "MM/dd/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy", "yyyy-MM-dd" };
                return DateTime.TryParseExact(texto.Trim(), formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
                    ? f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : "";
            default:
                return "";
        }
    }

    /// <summary>"Sí", "Si" o "No" a true o false; cualquier otra cosa ("Opcional", vacío) queda vacía.</summary>
    public static string SiNo(object? valor) => Texto(valor).ToLowerInvariant() switch
    {
        "sí" or "si" => "true",
        "no" => "false",
        _ => "",
    };

    /// <summary>Banderas guardadas como 0 o 1.</summary>
    public static string Bandera(object? valor) => Texto(valor) switch
    {
        "1" => "true",
        "0" => "false",
        _ => "",
    };

    public static string Decimal(object? valor)
    {
        var texto = Texto(valor);
        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero)
            ? numero.ToString(CultureInfo.InvariantCulture)
            : "";
    }

    public static string Entero(object? valor)
    {
        var texto = Texto(valor);
        return int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero)
            ? numero.ToString(CultureInfo.InvariantCulture)
            : "";
    }

    /// <summary>De "601, 603, 606,612" a "601,603,606,612".</summary>
    public static string Regimenes(object? valor) =>
        string.Join(',', System.Text.RegularExpressions.Regex.Matches(Texto(valor), @"\d{3}").Select(m => m.Value));

    /// <summary>
    /// El Excel nombra los impuestos y el CFDI usa su clave. Una fila del SAT dice "IVA CRÉDITO APLICADO DEL 50%":
    /// es la tasa del 8 % de la región fronteriza y sigue siendo IVA (002).
    /// </summary>
    public static string ClaveImpuesto(object? valor)
    {
        var texto = Texto(valor).ToUpperInvariant();
        return texto switch
        {
            "ISR" => "001",
            "IEPS" => "003",
            _ when texto.StartsWith("IVA", StringComparison.Ordinal) => "002",
            _ => texto,
        };
    }

    public static string Celdas(IExcelDataReader lector, int indice) =>
        indice < lector.FieldCount ? Texto(lector.GetValue(indice)) : "";
}
