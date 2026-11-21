using Comprobante.Core.Calculo;
using static Comprobante.UnitTests.Datos;

namespace Comprobante.UnitTests.Calculo;

/// <summary>
/// Los valores esperados salen de las fórmulas del Anexo 20 calculadas aparte, con aritmética decimal exacta
/// (módulo decimal de Python), no del propio código.
/// </summary>
public class LimitesTests
{
    [Fact]
    public void Importe_del_concepto_del_ejemplo_de_la_guia_cae_dentro_de_los_limites()
    {
        // Cantidad 3.141649 por valor unitario 1230.00 = 3864.22827 (Anexo 20, guía de llenado, concepto Importe).
        var rango = Limites.ImporteConcepto(3.141649m, 6, 1230.00m, 2, 5)!.Value;

        Assert.Equal(3864.21194m, rango.Inferior);
        Assert.Equal(3864.24460m, rango.Superior);
        Assert.True(rango.Contiene(3864.22827m));
    }

    [Fact]
    public void Con_cantidad_entera_el_margen_es_de_media_unidad()
    {
        // Sin decimales en la cantidad, la mitad de la última cifra es 0.5: el rango es amplio, como lo define el SAT.
        var rango = Limites.ImporteConcepto(1m, 0, 100.00m, 2, 2)!.Value;

        Assert.Equal(49.99m, rango.Inferior);
        Assert.Equal(150.01m, rango.Superior);
    }

    [Fact]
    public void Los_decimales_de_cada_campo_cambian_el_rango()
    {
        // 0.5 con un decimal por 33.333333 con seis: el rango es mucho más estrecho que con enteros.
        var rango = Limites.ImporteConcepto(0.5m, 1, 33.333333m, 6, 6)!.Value;

        Assert.Equal(14.999999m, rango.Inferior);
        Assert.Equal(18.333334m, rango.Superior);
    }

    [Theory]
    [InlineData("924.22", 2, "0.160000", 2, "147.87", "147.88")]
    [InlineData("3000.12827", 5, "0.160000", 6, "480.020522", "480.020524")]
    [InlineData("15000.00", 2, "0.106667", 2, "1600.00", "1600.01")]
    public void Importe_de_un_impuesto_de_concepto(
        string baseImpuesto, int decimalesBase, string tasa, int decimalesImporte, string inferior, string superior)
    {
        var rango = Limites.ImporteImpuesto(D(baseImpuesto), decimalesBase, D(tasa), decimalesImporte)!.Value;

        Assert.Equal(D(inferior), rango.Inferior);
        Assert.Equal(D(superior), rango.Superior);
    }

    [Fact]
    public void Contiene_incluye_los_dos_extremos_y_deja_fuera_lo_demas()
    {
        var rango = Limites.ImporteImpuesto(924.22m, 2, 0.16m, 2)!.Value;

        Assert.True(rango.Contiene(147.87m));
        Assert.True(rango.Contiene(147.88m));
        Assert.False(rango.Contiene(147.86m));
        Assert.False(rango.Contiene(147.89m));
    }

    [Fact]
    public void Con_cifras_que_no_caben_en_un_decimal_no_hay_rango()
    {
        Assert.Null(Limites.ImporteConcepto(999999999999999999.999999m, 6, 999999999999999999.999999m, 6, 6));
        Assert.Null(Limites.ImporteImpuesto(79000000000000000000000000.0m, 1, 99999999m, 2));
    }
}
