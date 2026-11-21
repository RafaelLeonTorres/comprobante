namespace Comprobante.Core.Catalogos;

/// <summary>
/// De dónde saca el generador y el validador los catálogos del SAT. La implementación de producción
/// (Comprobante.Catalogos) los lee de PostgreSQL con EF Core; las pruebas usan una en memoria.
/// </summary>
public interface ICatalogos
{
    /// <summary>Los catálogos chicos completos. La implementación los guarda en memoria hasta que cambia la versión.</summary>
    Task<CatalogosPequenos> PequenosAsync(CancellationToken cancelacion = default);

    /// <summary>Las claves de producto o servicio que existen entre las pedidas, con su registro.</summary>
    Task<IReadOnlyDictionary<string, ClaveProdServ>> ClavesProdServAsync(
        IReadOnlyCollection<string> claves, CancellationToken cancelacion = default);

    /// <summary>Las claves de unidad que existen entre las pedidas, con su registro.</summary>
    Task<IReadOnlyDictionary<string, ClaveUnidad>> ClavesUnidadAsync(
        IReadOnlyCollection<string> claves, CancellationToken cancelacion = default);

    /// <summary>Los códigos postales que existen entre los pedidos, con su registro.</summary>
    Task<IReadOnlyDictionary<string, CodigoPostal>> CodigosPostalesAsync(
        IReadOnlyCollection<string> codigos, CancellationToken cancelacion = default);
}
