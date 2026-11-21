using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Comprobante.IntegrationTests;

/// <summary>
/// La API contra un PostgreSQL 18 vacío y desechable (Testcontainers; necesita Docker). Al arrancar aplica las migraciones
/// y carga los catálogos del SAT, igual que en producción, y se comparte entre todas las pruebas de integración.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuracion) => configuracion.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalogos"] = _postgres.GetConnectionString(),
                // Sin el SQL de cada consulta en la salida de las pruebas.
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning",
            }));
    }

    public Task InitializeAsync() => _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionApi : ICollectionFixture<ApiFactory>
{
    public const string Nombre = "API con PostgreSQL";
}
