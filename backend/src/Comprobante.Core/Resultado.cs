using System.Diagnostics.CodeAnalysis;

namespace Comprobante.Core;

/// <summary>
/// Un dato de la solicitud que no se puede usar: el campo tal como lo escribe el JSON (<c>conceptos[0].cantidad</c>)
/// y qué hay que corregir, en español.
/// </summary>
public sealed record ErrorCampo(string Campo, string Mensaje);

/// <summary>El resultado de una operación que puede fallar por los datos de entrada: un valor o los errores por campo.</summary>
public sealed class Resultado<T>
{
    private Resultado(T? valor, IReadOnlyList<ErrorCampo> errores)
    {
        Valor = valor;
        Errores = errores;
    }

    public T? Valor { get; }

    public IReadOnlyList<ErrorCampo> Errores { get; }

    [MemberNotNullWhen(true, nameof(Valor))]
    public bool EsCorrecto => Errores.Count == 0;

    public static Resultado<T> Exito(T valor) => new(valor, []);

    public static Resultado<T> Fallo(IReadOnlyList<ErrorCampo> errores) => new(default, errores);
}
