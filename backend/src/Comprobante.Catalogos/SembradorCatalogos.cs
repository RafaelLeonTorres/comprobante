using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Comprobante.Catalogos;

/// <param name="Cargado">Falso si la base de datos ya tenía exactamente estos catálogos.</param>
/// <param name="Filas">Filas guardadas en esta carga (cero si no se cargó nada).</param>
public sealed record ResultadoSembrado(bool Cargado, string Huella, int Filas);

/// <summary>
/// Aplica las migraciones y carga a PostgreSQL los catálogos del SAT que vienen en el ensamblado. Se puede llamar en cada
/// arranque de la API: si la huella SHA-256 de los CSV es la misma que la última carga, no hace nada. La carga corre en una
/// transacción con un candado de PostgreSQL, así dos instancias de la API que arrancan juntas no se pisan.
/// </summary>
public sealed class SembradorCatalogos(
    IDbContextFactory<CatalogosDbContext> fabrica,
    CatalogosEf catalogos,
    ILogger<SembradorCatalogos> log)
{
    // Número cualquiera, siempre el mismo, para el candado de pg_advisory_xact_lock.
    private const long LlaveCandado = 0x43464449_34303030;

    private const int Lote = 5000;

    public async Task<ResultadoSembrado> SembrarAsync(CancellationToken cancelacion = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        await db.Database.MigrateAsync(cancelacion);

        var huella = RecursosCatalogos.Huella();
        if (await YaCargadoAsync(db, huella, cancelacion))
        {
            log.LogInformation("Los catálogos del SAT ya están al día (huella {Huella}).", huella[..12]);
            return new ResultadoSembrado(false, huella, 0);
        }

        var reloj = Stopwatch.StartNew();
        var (archivo, publicacion) = RecursosCatalogos.LeerVersion();
        log.LogInformation("Cargando los catálogos del SAT ({Archivo})...", archivo);

        await using var transaccion = await db.Database.BeginTransactionAsync(cancelacion);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({LlaveCandado})", cancelacion);

        // Otra instancia pudo cargarlos mientras esperábamos el candado.
        if (await YaCargadoAsync(db, huella, cancelacion))
        {
            return new ResultadoSembrado(false, huella, 0);
        }

        var filas = 0;
        filas += await ReemplazarAsync(db, db.FormasPago, CargadorCsv.FormasPago(), cancelacion);
        filas += await ReemplazarAsync(db, db.Monedas, CargadorCsv.Monedas(), cancelacion);
        filas += await ReemplazarAsync(db, db.TiposDeComprobante, CargadorCsv.TiposDeComprobante(), cancelacion);
        filas += await ReemplazarAsync(db, db.Exportaciones, CargadorCsv.Exportaciones(), cancelacion);
        filas += await ReemplazarAsync(db, db.MetodosPago, CargadorCsv.MetodosPago(), cancelacion);
        filas += await ReemplazarAsync(db, db.CodigosPostales, CargadorCsv.CodigosPostales(), cancelacion);
        filas += await ReemplazarAsync(db, db.Periodicidades, CargadorCsv.Periodicidades(), cancelacion);
        filas += await ReemplazarAsync(db, db.Meses, CargadorCsv.Meses(), cancelacion);
        filas += await ReemplazarAsync(db, db.TiposRelacion, CargadorCsv.TiposRelacion(), cancelacion);
        filas += await ReemplazarAsync(db, db.RegimenesFiscales, CargadorCsv.RegimenesFiscales(), cancelacion);
        filas += await ReemplazarAsync(db, db.Paises, CargadorCsv.Paises(), cancelacion);
        filas += await ReemplazarAsync(db, db.UsosCfdi, CargadorCsv.UsosCfdi(), cancelacion);
        filas += await ReemplazarAsync(db, db.ClavesProdServ, CargadorCsv.ClavesProdServ(), cancelacion);
        filas += await ReemplazarAsync(db, db.ClavesUnidad, CargadorCsv.ClavesUnidad(), cancelacion);
        filas += await ReemplazarAsync(db, db.ObjetosImp, CargadorCsv.ObjetosImp(), cancelacion);
        filas += await ReemplazarAsync(db, db.Impuestos, CargadorCsv.Impuestos(), cancelacion);
        filas += await ReemplazarAsync(db, db.TiposFactor, CargadorCsv.TiposFactor(), cancelacion);
        filas += await ReemplazarAsync(db, db.TasasOCuotas, CargadorCsv.TasasOCuotas(), cancelacion);
        filas += await ReemplazarAsync(db, db.Estados, CargadorCsv.Estados(), cancelacion);

        db.VersionesCatalogos.Add(new VersionCatalogos
        {
            Archivo = archivo,
            Publicacion = publicacion,
            Sha256 = huella,
            CargadoEn = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        catalogos.Invalidar();
        log.LogInformation("Catálogos del SAT cargados: {Filas:N0} filas en {Segundos:N1} s.", filas, reloj.Elapsed.TotalSeconds);
        return new ResultadoSembrado(true, huella, filas);
    }

    private static async Task<bool> YaCargadoAsync(CatalogosDbContext db, string huella, CancellationToken cancelacion) =>
        await db.VersionesCatalogos.AsNoTracking()
            .OrderByDescending(v => v.Id)
            .Select(v => v.Sha256)
            .FirstOrDefaultAsync(cancelacion) == huella;

    /// <summary>Vacía la tabla y la llena por lotes: un lote por SaveChanges, para no acumular 95 mil objetos en el contexto.</summary>
    private static async Task<int> ReemplazarAsync<T>(
        CatalogosDbContext db, DbSet<T> tabla, List<T> registros, CancellationToken cancelacion)
        where T : class
    {
        await tabla.ExecuteDeleteAsync(cancelacion);
        for (var desde = 0; desde < registros.Count; desde += Lote)
        {
            tabla.AddRange(registros.GetRange(desde, Math.Min(Lote, registros.Count - desde)));
            await db.SaveChangesAsync(cancelacion);
            db.ChangeTracker.Clear();
        }

        return registros.Count;
    }
}
