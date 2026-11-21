using Comprobante.Core.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Comprobante.Catalogos;

public static class CatalogosServiceCollectionExtensions
{
    /// <summary>
    /// Registra los catálogos del SAT: el contexto de EF Core sobre PostgreSQL, <see cref="ICatalogos"/> y el sembrador.
    /// </summary>
    public static IServiceCollection AddCatalogosSat(this IServiceCollection servicios, string cadenaConexion)
    {
        servicios.AddDbContextFactory<CatalogosDbContext>(opciones =>
            opciones.UseNpgsql(cadenaConexion, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history")));
        servicios.AddSingleton<CatalogosEf>();
        servicios.AddSingleton<ICatalogos>(proveedor => proveedor.GetRequiredService<CatalogosEf>());
        servicios.AddSingleton<SembradorCatalogos>();
        return servicios;
    }
}
