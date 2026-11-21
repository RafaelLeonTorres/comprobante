using Comprobante.Catalogos;
using Comprobante.Core.Catalogos;

namespace Comprobante.UnitTests;

/// <summary>Los catálogos del SAT leídos de los CSV una sola vez para todas las pruebas que los usan.</summary>
public sealed class CatalogosFixture : IAsyncLifetime
{
    public CatalogosEnMemoria Catalogos { get; } = new();

    public CatalogosPequenos Pequenos { get; private set; } = null!;

    public async Task InitializeAsync() => Pequenos = await Catalogos.PequenosAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionCatalogos : ICollectionFixture<CatalogosFixture>
{
    public const string Nombre = "Catálogos del SAT";
}
