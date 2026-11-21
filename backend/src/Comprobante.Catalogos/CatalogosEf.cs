using Comprobante.Core.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Comprobante.Catalogos;

/// <summary>
/// <see cref="ICatalogos"/> sobre PostgreSQL con EF Core. Los catálogos chicos se leen completos una vez y se quedan
/// en memoria; los grandes se consultan por clave con <c>WHERE clave = ANY(...)</c>.
/// </summary>
public sealed class CatalogosEf(IDbContextFactory<CatalogosDbContext> fabrica) : ICatalogos
{
    private readonly object _candado = new();
    private Task<CatalogosPequenos>? _pequenos;

    public Task<CatalogosPequenos> PequenosAsync(CancellationToken cancelacion = default)
    {
        lock (_candado)
        {
            // La carga compartida no depende de quien la pidió primero: si ese cancela, los demás no deben fallar.
            return _pequenos ??= CargarPequenosAsync();
        }
    }

    /// <summary>Descarta lo guardado en memoria: lo llama el sembrador cuando carga catálogos nuevos.</summary>
    public void Invalidar()
    {
        lock (_candado)
        {
            _pequenos = null;
        }
    }

    private async Task<CatalogosPequenos> CargarPequenosAsync()
    {
        try
        {
            await using var db = await fabrica.CreateDbContextAsync();
            db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return new CatalogosPequenos(
                await db.FormasPago.ToListAsync(),
                await db.Monedas.ToListAsync(),
                await db.TiposDeComprobante.ToListAsync(),
                await db.Exportaciones.ToListAsync(),
                await db.MetodosPago.ToListAsync(),
                await db.Periodicidades.ToListAsync(),
                await db.Meses.ToListAsync(),
                await db.TiposRelacion.ToListAsync(),
                await db.RegimenesFiscales.ToListAsync(),
                await db.Paises.ToListAsync(),
                await db.UsosCfdi.ToListAsync(),
                await db.ObjetosImp.ToListAsync(),
                await db.Impuestos.ToListAsync(),
                await db.TiposFactor.ToListAsync(),
                await db.TasasOCuotas.ToListAsync(),
                await db.Estados.ToListAsync());
        }
        catch
        {
            Invalidar(); // si falló, la siguiente consulta lo intenta de nuevo
            throw;
        }
    }

    public async Task<IReadOnlyDictionary<string, ClaveProdServ>> ClavesProdServAsync(
        IReadOnlyCollection<string> claves, CancellationToken cancelacion = default)
    {
        if (claves.Count == 0)
        {
            return new Dictionary<string, ClaveProdServ>();
        }

        var pedidas = claves.Distinct().ToArray();
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);
        return await db.ClavesProdServ.AsNoTracking()
            .Where(x => pedidas.Contains(x.Clave))
            .ToDictionaryAsync(x => x.Clave, cancelacion);
    }

    public async Task<IReadOnlyDictionary<string, ClaveUnidad>> ClavesUnidadAsync(
        IReadOnlyCollection<string> claves, CancellationToken cancelacion = default)
    {
        if (claves.Count == 0)
        {
            return new Dictionary<string, ClaveUnidad>();
        }

        var pedidas = claves.Distinct().ToArray();
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);
        return await db.ClavesUnidad.AsNoTracking()
            .Where(x => pedidas.Contains(x.Clave))
            .ToDictionaryAsync(x => x.Clave, cancelacion);
    }

    public async Task<IReadOnlyDictionary<string, CodigoPostal>> CodigosPostalesAsync(
        IReadOnlyCollection<string> codigos, CancellationToken cancelacion = default)
    {
        if (codigos.Count == 0)
        {
            return new Dictionary<string, CodigoPostal>();
        }

        var pedidos = codigos.Distinct().ToArray();
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);
        return await db.CodigosPostales.AsNoTracking()
            .Where(x => pedidos.Contains(x.Clave))
            .ToDictionaryAsync(x => x.Clave, cancelacion);
    }
}
