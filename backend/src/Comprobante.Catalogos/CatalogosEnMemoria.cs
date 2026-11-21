using Comprobante.Core.Catalogos;

namespace Comprobante.Catalogos;

/// <summary>
/// <see cref="ICatalogos"/> que lee los CSV directo, sin base de datos: para las pruebas del núcleo y para quien quiera
/// usar el generador y el validador como librería. Carga cada catálogo grande la primera vez que se pide.
/// </summary>
public sealed class CatalogosEnMemoria : ICatalogos
{
    private readonly Lazy<CatalogosPequenos> _pequenos = new(CargadorCsv.Pequenos);
    private readonly Lazy<Dictionary<string, ClaveProdServ>> _productos =
        new(() => CargadorCsv.ClavesProdServ().ToDictionary(x => x.Clave, StringComparer.Ordinal));
    private readonly Lazy<Dictionary<string, ClaveUnidad>> _unidades =
        new(() => CargadorCsv.ClavesUnidad().ToDictionary(x => x.Clave, StringComparer.Ordinal));
    private readonly Lazy<Dictionary<string, CodigoPostal>> _codigos =
        new(() => CargadorCsv.CodigosPostales().ToDictionary(x => x.Clave, StringComparer.Ordinal));

    public Task<CatalogosPequenos> PequenosAsync(CancellationToken cancelacion = default) =>
        Task.FromResult(_pequenos.Value);

    public Task<IReadOnlyDictionary<string, ClaveProdServ>> ClavesProdServAsync(
        IReadOnlyCollection<string> claves, CancellationToken cancelacion = default) =>
        Task.FromResult(Filtrar(_productos.Value, claves));

    public Task<IReadOnlyDictionary<string, ClaveUnidad>> ClavesUnidadAsync(
        IReadOnlyCollection<string> claves, CancellationToken cancelacion = default) =>
        Task.FromResult(Filtrar(_unidades.Value, claves));

    public Task<IReadOnlyDictionary<string, CodigoPostal>> CodigosPostalesAsync(
        IReadOnlyCollection<string> codigos, CancellationToken cancelacion = default) =>
        Task.FromResult(Filtrar(_codigos.Value, codigos));

    private static IReadOnlyDictionary<string, T> Filtrar<T>(Dictionary<string, T> todos, IReadOnlyCollection<string> claves)
    {
        var resultado = new Dictionary<string, T>();
        foreach (var clave in claves)
        {
            if (todos.TryGetValue(clave, out var registro))
            {
                resultado[clave] = registro;
            }
        }

        return resultado;
    }
}
