using System.Text.RegularExpressions;

namespace Comprobante.Catalogos;

/// <summary>Una fila de la matriz de errores del SAT: dónde se revisa, qué se revisa y con qué código se rechaza.</summary>
/// <param name="Elemento">El nodo del CFDI al que corresponde la regla (por ejemplo <c>cfdi:Receptor</c>).</param>
/// <param name="Atributo">El atributo que revisa, o vacío si la regla es del nodo completo.</param>
public sealed record ErrorDeMatriz(int Numero, string Elemento, string Atributo, string Codigo, string Descripcion);

/// <summary>
/// La matriz de errores del SAT (<c>sat/matriz/matriz-errores.csv</c>), que viaja como recurso incrustado del ensamblado
/// igual que los catálogos. Es la lista de reglas que un PAC aplica al timbrar un CFDI 4.0.
/// </summary>
public static partial class MatrizDeErrores
{
    private const string Recurso = "matriz/matriz-errores.csv";

    private static readonly Lazy<IReadOnlyList<ErrorDeMatriz>> Cargada = new(Leer);

    /// <summary>Todas las reglas, en el orden de la matriz.</summary>
    public static IReadOnlyList<ErrorDeMatriz> Todos => Cargada.Value;

    private static List<ErrorDeMatriz> Leer()
    {
        using var flujo = typeof(MatrizDeErrores).Assembly.GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException("El ensamblado no trae la matriz de errores del SAT.");
        return LectorCsv.Leer(flujo, "matriz-errores.csv")
            .Select(fila => new ErrorDeMatriz(
                fila.Entero("numero"),
                fila.TextoOpcional("elemento") ?? string.Empty,
                fila.TextoOpcional("atributo") ?? string.Empty,
                fila.Texto("codigo"),
                Espacios().Replace(fila.Texto("descripcion"), " ").Trim()))
            .ToList();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Espacios();
}
