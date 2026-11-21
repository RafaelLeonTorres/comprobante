using Comprobante.Core.Generacion;

namespace Comprobante.UnitTests.Generacion;

public class ZonaHorariaTests
{
    // 21 de noviembre de 2026, 15:30 UTC: en el centro del país son las 09:30 (UTC-6, sin horario de verano desde 2022).
    private static readonly DateTimeOffset Noviembre = new(2026, 11, 21, 15, 30, 0, TimeSpan.Zero);

    // 15 de julio de 2026, 15:30 UTC: la frontera sigue el horario de verano de Estados Unidos.
    private static readonly DateTimeOffset Julio = new(2026, 7, 15, 15, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Tiempo del Centro", "America/Mexico_City")]
    [InlineData("Tiempo del Pacífico", "America/Mazatlan")]
    [InlineData("Tiempo del Noroeste", "America/Tijuana")]
    [InlineData("Tiempo del Sureste", "America/Cancun")]
    [InlineData("Tiempo del Centro en Frontera", "America/Matamoros")]
    public void Cada_huso_del_catalogo_tiene_su_zona(string huso, string zona) => Assert.Equal(zona, ZonaHoraria.IdDe(huso));

    [Theory]
    [InlineData("Tiempo del Centro", "2026-11-21T09:30:00", "2026-07-15T09:30:00")]
    [InlineData("Tiempo del Pacífico", "2026-11-21T08:30:00", "2026-07-15T08:30:00")]
    [InlineData("Tiempo del Noroeste", "2026-11-21T07:30:00", "2026-07-15T08:30:00")]
    [InlineData("Tiempo del Sureste", "2026-11-21T10:30:00", "2026-07-15T10:30:00")]
    [InlineData("Tiempo del Centro en Frontera", "2026-11-21T09:30:00", "2026-07-15T10:30:00")]
    public void La_hora_local_sigue_el_horario_de_cada_zona(string huso, string enNoviembre, string enJulio)
    {
        Assert.Equal(enNoviembre, ZonaHoraria.ComoFecha(ZonaHoraria.Local(Noviembre, huso)));
        Assert.Equal(enJulio, ZonaHoraria.ComoFecha(ZonaHoraria.Local(Julio, huso)));
    }

    [Fact]
    public void Un_huso_desconocido_cae_en_la_hora_del_centro() =>
        Assert.Equal("America/Mexico_City", ZonaHoraria.IdDe("Tiempo de la Luna"));

    [Fact]
    public void La_fecha_se_escribe_como_la_pide_el_CFDI_sin_fracciones_ni_zona()
    {
        var fecha = new DateTime(2026, 3, 5, 7, 8, 9, 987, DateTimeKind.Utc);

        Assert.Equal("2026-03-05T07:08:09", ZonaHoraria.ComoFecha(fecha));
    }
}
