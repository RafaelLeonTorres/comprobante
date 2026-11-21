using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Comprobante.Catalogos;

/// <summary>
/// Para que "dotnet ef migrations add" cree el contexto sin arrancar la API. No se conecta a nada: solo necesita
/// saber que el proveedor es PostgreSQL.
/// </summary>
public sealed class CatalogosDbContextFactory : IDesignTimeDbContextFactory<CatalogosDbContext>
{
    public CatalogosDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CatalogosDbContext>()
            .UseNpgsql("Host=localhost;Database=comprobante;Username=postgres", n => n.MigrationsHistoryTable("__ef_migrations_history"))
            .Options);
}
