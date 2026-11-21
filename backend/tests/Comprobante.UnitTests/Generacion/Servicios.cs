using Comprobante.Core.Calculo;
using Comprobante.Core.Catalogos;
using Comprobante.Core.Generacion;
using Comprobante.Core.Sello;
using Comprobante.Core.Validacion;

namespace Comprobante.UnitTests.Generacion;

/// <summary>Un reloj que siempre marca la misma hora, para que la fecha por omisión del comprobante sea predecible.</summary>
internal sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;
}

/// <summary>Arma los servicios del generador con los catálogos de la prueba, como lo haría la inyección de dependencias.</summary>
internal static class Servicios
{
    /// <summary>21 de noviembre de 2026, 15:30 UTC: las 09:30 en el centro del país.</summary>
    public static readonly DateTimeOffset Ahora = new(2026, 11, 21, 15, 30, 0, TimeSpan.Zero);

    public static ServicioGeneracion Generacion(ICatalogos catalogos, DateTimeOffset? ahora = null) =>
        new(catalogos, new ServicioCalculo(catalogos), new EmisoresDePrueba(), Validador(catalogos, ahora), new RelojFijo(ahora ?? Ahora));

    public static ValidadorCfdi Validador(ICatalogos catalogos, DateTimeOffset? ahora = null) =>
        new(catalogos, new RelojFijo(ahora ?? Ahora));
}
