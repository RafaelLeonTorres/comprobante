using Comprobante.Core;
using Comprobante.Core.Calculo;
using static Comprobante.UnitTests.Datos;

namespace Comprobante.UnitTests.Calculo;

public class RedondeoTests
{
    [Theory]
    [InlineData("924.225", 2, "924.23")]
    [InlineData("924.224", 2, "924.22")]
    [InlineData("924.2249999", 2, "924.22")]
    [InlineData("0.5", 0, "1")]
    [InlineData("2.5", 0, "3")] // no es el redondeo bancario: el 5 siempre sube
    [InlineData("100", 2, "100")]
    public void Redondear_hace_subir_el_cinco(string valor, int decimales, string esperado) =>
        Assert.Equal(D(esperado), Redondeo.Redondear(D(valor), decimales));

    [Theory]
    [InlineData("924.224956", 2, "924.22", "924.23")] // el ejemplo del Anexo 20 con moneda MXN
    [InlineData("281.594680", 3, "281.594", "281.595")] // y con moneda OMR (tres decimales)
    [InlineData("924.22", 2, "924.22", "924.22")] // si ya cabe en los decimales no cambia
    [InlineData("924.220000001", 2, "924.22", "924.23")] // cualquier resto sube el límite superior
    [InlineData("0.000001", 2, "0", "0.01")]
    public void Truncar_y_subir_son_los_dos_extremos(string valor, int decimales, string truncado, string haciaArriba)
    {
        Assert.Equal(D(truncado), Redondeo.Truncar(D(valor), decimales));
        Assert.Equal(D(haciaArriba), Redondeo.HaciaArriba(D(valor), decimales));
    }

    [Theory]
    [InlineData("1", 0)]
    [InlineData("100.00", 0)]
    [InlineData("1.50", 1)]
    [InlineData("3.141649", 6)]
    [InlineData("0.000001", 6)]
    [InlineData("0", 0)]
    public void Decimales_no_cuenta_los_ceros_de_la_derecha(string valor, int esperado) =>
        Assert.Equal(esperado, Redondeo.Decimales(D(valor)));

    [Theory]
    [InlineData("3", 0)]
    [InlineData("3.10", 2)]
    [InlineData("3.000000", 6)]
    [InlineData("0.5", 1)]
    public void DecimalesEscritos_si_cuenta_los_ceros(string texto, int esperado) =>
        Assert.Equal(esperado, Redondeo.DecimalesEscritos(texto));

    [Theory]
    [InlineData("1230", 2, "1230.00")]
    [InlineData("3864.22827", 2, "3864.22827")]
    [InlineData("0.1", 2, "0.10")]
    [InlineData("5", 0, "5")]
    [InlineData("0.160000", 6, "0.160000")]
    [InlineData("1234.5", 0, "1234.5")]
    public void El_formato_del_xml_pone_punto_decimal_y_no_deja_ceros_de_mas(string valor, int minimo, string esperado) =>
        Assert.Equal(esperado, Formato.Importe(D(valor), minimo));
}
