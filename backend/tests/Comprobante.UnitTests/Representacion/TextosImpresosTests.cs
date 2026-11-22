using Comprobante.Core.Representacion;

namespace Comprobante.UnitTests.Representacion;

public class TextosImpresosTests
{
    [Theory]
    [InlineData("1234567.891234", "1,234,567.891234")]
    [InlineData("999", "999")]
    [InlineData("1000", "1,000")]
    [InlineData("100000.5", "100,000.5")]
    [InlineData("0.5", "0.5")]
    [InlineData("-1234.5", "-1,234.5")]
    [InlineData("12", "12")]
    [InlineData("abc", "abc")] // lo que no es un número se deja como viene
    [InlineData("1,5", "1,5")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("", "—")]
    [InlineData(null, "—")]
    public void Agrupar_pone_las_comas_de_miles_y_respeta_los_decimales(string? numero, string esperado) =>
        Assert.Equal(esperado, TextosImpresos.Agrupar(numero));

    [Theory]
    [InlineData("10", "10.00")]
    [InlineData("850.5", "850.50")]
    [InlineData("850.00", "850.00")]
    [InlineData("3.141649", "3.141649")]
    [InlineData("1000000", "1,000,000.00")]
    [InlineData("896.0028", "896.0028")]
    [InlineData("0", "0.00")]
    [InlineData("abc", "abc")]
    public void AgruparConDosDecimales_no_pierde_los_decimales_que_trae_el_XML(string numero, string esperado) =>
        Assert.Equal(esperado, TextosImpresos.AgruparConDosDecimales(numero));

    [Theory]
    [InlineData("0.160000", "16.00")]
    [InlineData("0.106667", "10.6667")]
    [InlineData("0.000000", "0.00")]
    [InlineData("0.265000", "26.50")]
    [InlineData("0.080000", "8.00")]
    [InlineData("1.000000", "100.00")]
    [InlineData(null, "—")]
    [InlineData("x", "—")]
    public void Porcentaje_pasa_la_tasa_del_XML_a_porcentaje(string? tasa, string esperado) =>
        Assert.Equal(esperado, TextosImpresos.Porcentaje(tasa));

    [Theory]
    [InlineData("1200", "USD", "$1,200.00 USD")]
    [InlineData("1200", "MXN", "$1,200.00")]
    [InlineData("1200", "XXX", "$1,200.00")]
    [InlineData("0", "EUR", "$0.00 EUR")]
    [InlineData(null, "MXN", "—")]
    public void DineroConMoneda_agrega_la_clave_solo_cuando_no_es_peso(string? importe, string moneda, string esperado) =>
        Assert.Equal(esperado, TextosImpresos.DineroConMoneda(importe, moneda));

    [Theory]
    [InlineData("G03", "Gastos en general", "G03 - Gastos en general")]
    [InlineData("G03", null, "G03")]
    [InlineData("G03", "", "G03")]
    [InlineData(null, "Gastos en general", "—")]
    [InlineData("", null, "—")]
    public void Con_junta_la_clave_y_su_descripcion(string? clave, string? descripcion, string esperado) =>
        Assert.Equal(esperado, TextosImpresos.Con(clave, descripcion));

    [Fact]
    public void SinEspacios_quita_espacios_y_saltos_de_linea_de_un_sello()
    {
        Assert.Equal("abcDEF==", TextosImpresos.SinEspacios("abc\n DEF\r\n=="));
        Assert.Equal(string.Empty, TextosImpresos.SinEspacios(null));
    }
}
