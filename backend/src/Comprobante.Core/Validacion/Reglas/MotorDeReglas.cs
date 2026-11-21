namespace Comprobante.Core.Validacion.Reglas;

/// <summary>Ejecuta, en orden, los grupos de reglas de la matriz de errores del SAT sobre un comprobante.</summary>
internal static class MotorDeReglas
{
    private static readonly (string Nombre, Action<ContextoValidacion> Revisar)[] Grupos =
    [
        ("el comprobante", ReglasComprobante.Revisar),
        ("el emisor y el receptor", ReglasPartes.Revisar),
        ("los conceptos", ReglasConceptos.Revisar),
        ("los impuestos", ReglasImpuestos.Revisar),
        ("los complementos", ReglasComplementos.Revisar),
    ];

    /// <summary>
    /// Revisa el comprobante con todos los grupos. Si uno falla por un error inesperado del validador, se informa con el código
    /// de error no clasificado del SAT y los demás grupos se revisan igual: es mejor decir que algo no se pudo revisar que
    /// darlo por bueno.
    /// </summary>
    public static IReadOnlyList<Hallazgo> Revisar(
        ContextoValidacion contexto, IEnumerable<(string Nombre, Action<ContextoValidacion> Revisar)>? grupos = null)
    {
        foreach (var (nombre, revisar) in grupos ?? Grupos)
        {
            try
            {
                revisar(contexto);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                contexto.Error(
                    ReglasSat.NoClasificado,
                    contexto.Raiz,
                    null,
                    $"No se pudieron revisar las reglas de {nombre} por un error inesperado del validador; las demás sí se revisaron.");
            }
        }

        return contexto.Hallazgos;
    }
}
