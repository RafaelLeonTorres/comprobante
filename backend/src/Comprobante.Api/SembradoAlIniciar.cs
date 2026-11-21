using Comprobante.Catalogos;

namespace Comprobante.Api;

/// <summary>
/// Aplica las migraciones y carga los catálogos del SAT antes de que la API empiece a recibir peticiones: ASP.NET Core
/// espera a que terminen los servicios de arranque, así el contenedor no queda "healthy" hasta tener los catálogos.
/// </summary>
internal sealed class SembradoAlIniciar(SembradorCatalogos sembrador) : IHostedService
{
    public async Task StartAsync(CancellationToken cancelacion) => await sembrador.SembrarAsync(cancelacion);

    public Task StopAsync(CancellationToken cancelacion) => Task.CompletedTask;
}
