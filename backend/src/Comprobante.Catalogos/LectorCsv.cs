using System.Globalization;
using System.Text;

namespace Comprobante.Catalogos;

/// <summary>Una fila de un CSV con sus columnas accesibles por el nombre del encabezado.</summary>
internal sealed class FilaCsv(IReadOnlyDictionary<string, int> columnas, string[] celdas, string archivo, int numero)
{
    private string? Celda(string columna)
    {
        if (!columnas.TryGetValue(columna, out var indice))
        {
            throw new InvalidDataException($"{archivo}: no existe la columna \"{columna}\".");
        }

        return indice < celdas.Length && celdas[indice].Length > 0 ? celdas[indice] : null;
    }

    private InvalidDataException Error(string columna, string motivo) =>
        new($"{archivo}, fila {numero}, columna \"{columna}\": {motivo}.");

    public string Texto(string columna) => Celda(columna) ?? throw Error(columna, "está vacía");

    public string? TextoOpcional(string columna) => Celda(columna);

    public bool Booleano(string columna) =>
        BooleanoOpcional(columna) ?? throw Error(columna, "está vacía");

    public bool? BooleanoOpcional(string columna) => Celda(columna) switch
    {
        null => null,
        "true" => true,
        "false" => false,
        var otro => throw Error(columna, $"se esperaba true o false y dice \"{otro}\""),
    };

    public int Entero(string columna) =>
        EnteroOpcional(columna) ?? throw Error(columna, "está vacía");

    public int? EnteroOpcional(string columna)
    {
        var texto = Celda(columna);
        if (texto is null)
        {
            return null;
        }

        return int.TryParse(texto, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : throw Error(columna, $"\"{texto}\" no es un entero");
    }

    public decimal Decimal(string columna) =>
        DecimalOpcional(columna) ?? throw Error(columna, "está vacía");

    public decimal? DecimalOpcional(string columna)
    {
        var texto = Celda(columna);
        if (texto is null)
        {
            return null;
        }

        return decimal.TryParse(texto, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;
    }

    public DateOnly Fecha(string columna) =>
        FechaOpcional(columna) ?? throw Error(columna, "está vacía");

    public DateOnly? FechaOpcional(string columna)
    {
        var texto = Celda(columna);
        if (texto is null)
        {
            return null;
        }

        return DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var valor)
            ? valor
            : throw Error(columna, $"\"{texto}\" no es una fecha yyyy-MM-dd");
    }

    /// <summary>Una lista separada por comas ("601,603,606").</summary>
    public string[] Lista(string columna) =>
        Celda(columna)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
}

/// <summary>Lee los CSV que genera Comprobante.Herramientas: UTF-8, separados por comas, con comillas dobles (RFC 4180).</summary>
internal static class LectorCsv
{
    public static IEnumerable<FilaCsv> Leer(Stream flujo, string archivo)
    {
        using var lector = new StreamReader(flujo, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        var encabezado = LeerRegistro(lector);
        if (encabezado is null)
        {
            yield break;
        }

        var columnas = encabezado
            .Select((nombre, indice) => (nombre, indice))
            .ToDictionary(x => x.nombre, x => x.indice, StringComparer.Ordinal);

        var numero = 1;
        while (LeerRegistro(lector) is { } celdas)
        {
            numero++;
            if (celdas.Length == 1 && celdas[0].Length == 0)
            {
                continue; // renglón en blanco
            }

            yield return new FilaCsv(columnas, celdas, archivo, numero);
        }
    }

    /// <summary>Un registro: puede ocupar varias líneas si un campo entre comillas trae saltos de línea.</summary>
    private static string[]? LeerRegistro(TextReader lector)
    {
        var siguiente = lector.Read();
        if (siguiente < 0)
        {
            return null;
        }

        var celdas = new List<string>();
        var actual = new StringBuilder();
        var entreComillas = false;
        var caracter = siguiente;
        while (caracter >= 0)
        {
            var c = (char)caracter;
            if (entreComillas)
            {
                if (c == '"')
                {
                    if (lector.Peek() == '"')
                    {
                        lector.Read();
                        actual.Append('"');
                    }
                    else
                    {
                        entreComillas = false;
                    }
                }
                else
                {
                    actual.Append(c);
                }
            }
            else if (c == '"')
            {
                entreComillas = true;
            }
            else if (c == ',')
            {
                celdas.Add(actual.ToString());
                actual.Clear();
            }
            else if (c == '\n')
            {
                break;
            }
            else if (c != '\r')
            {
                actual.Append(c);
            }

            caracter = lector.Read();
        }

        celdas.Add(actual.ToString());
        return celdas.ToArray();
    }
}
