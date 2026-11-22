using Comprobante.Core.Representacion;
using static Comprobante.UnitTests.Datos;

namespace Comprobante.UnitTests.Representacion;

public class ImporteConLetraTests
{
    [Theory]
    [InlineData("0", "CERO PESOS 00/100 M.N.")]
    [InlineData("1", "UN PESO 00/100 M.N.")]
    [InlineData("2", "DOS PESOS 00/100 M.N.")]
    [InlineData("10", "DIEZ PESOS 00/100 M.N.")]
    [InlineData("15", "QUINCE PESOS 00/100 M.N.")]
    [InlineData("16", "DIECISÉIS PESOS 00/100 M.N.")]
    [InlineData("20", "VEINTE PESOS 00/100 M.N.")]
    [InlineData("21", "VEINTIÚN PESOS 00/100 M.N.")]
    [InlineData("22", "VEINTIDÓS PESOS 00/100 M.N.")]
    [InlineData("23", "VEINTITRÉS PESOS 00/100 M.N.")]
    [InlineData("26", "VEINTISÉIS PESOS 00/100 M.N.")]
    [InlineData("29", "VEINTINUEVE PESOS 00/100 M.N.")]
    [InlineData("30", "TREINTA PESOS 00/100 M.N.")]
    [InlineData("31", "TREINTA Y UN PESOS 00/100 M.N.")]
    [InlineData("45", "CUARENTA Y CINCO PESOS 00/100 M.N.")]
    [InlineData("99", "NOVENTA Y NUEVE PESOS 00/100 M.N.")]
    public void Escribe_hasta_cien(string importe, string esperado) =>
        Assert.Equal(esperado, ImporteConLetra.De(D(importe), "MXN"));

    [Theory]
    [InlineData("100", "CIEN PESOS 00/100 M.N.")]
    [InlineData("101", "CIENTO UN PESOS 00/100 M.N.")]
    [InlineData("115", "CIENTO QUINCE PESOS 00/100 M.N.")]
    [InlineData("121", "CIENTO VEINTIÚN PESOS 00/100 M.N.")]
    [InlineData("200", "DOSCIENTOS PESOS 00/100 M.N.")]
    [InlineData("500", "QUINIENTOS PESOS 00/100 M.N.")]
    [InlineData("999", "NOVECIENTOS NOVENTA Y NUEVE PESOS 00/100 M.N.")]
    public void Escribe_las_centenas(string importe, string esperado) =>
        Assert.Equal(esperado, ImporteConLetra.De(D(importe), "MXN"));

    [Theory]
    [InlineData("1000", "MIL PESOS 00/100 M.N.")]
    [InlineData("1001", "MIL UN PESOS 00/100 M.N.")]
    [InlineData("1100", "MIL CIEN PESOS 00/100 M.N.")]
    [InlineData("2000", "DOS MIL PESOS 00/100 M.N.")]
    [InlineData("10708.48", "DIEZ MIL SETECIENTOS OCHO PESOS 48/100 M.N.")] // el del boceto de la plantilla
    [InlineData("21000", "VEINTIÚN MIL PESOS 00/100 M.N.")]
    [InlineData("31000", "TREINTA Y UN MIL PESOS 00/100 M.N.")]
    [InlineData("100000", "CIEN MIL PESOS 00/100 M.N.")]
    [InlineData("101000", "CIENTO UN MIL PESOS 00/100 M.N.")]
    [InlineData("101001", "CIENTO UN MIL UN PESOS 00/100 M.N.")]
    [InlineData("999999.99", "NOVECIENTOS NOVENTA Y NUEVE MIL NOVECIENTOS NOVENTA Y NUEVE PESOS 99/100 M.N.")]
    public void Escribe_los_miles(string importe, string esperado) =>
        Assert.Equal(esperado, ImporteConLetra.De(D(importe), "MXN"));

    [Theory]
    [InlineData("1000000", "UN MILLÓN DE PESOS 00/100 M.N.")]
    [InlineData("1000000.50", "UN MILLÓN DE PESOS 50/100 M.N.")]
    [InlineData("1000001", "UN MILLÓN UN PESOS 00/100 M.N.")]
    [InlineData("1200000", "UN MILLÓN DOSCIENTOS MIL PESOS 00/100 M.N.")]
    [InlineData("2000000", "DOS MILLONES DE PESOS 00/100 M.N.")]
    [InlineData("2500000", "DOS MILLONES QUINIENTOS MIL PESOS 00/100 M.N.")]
    [InlineData("21000000", "VEINTIÚN MILLONES DE PESOS 00/100 M.N.")]
    [InlineData("123456789", "CIENTO VEINTITRÉS MILLONES CUATROCIENTOS CINCUENTA Y SEIS MIL SETECIENTOS OCHENTA Y NUEVE PESOS 00/100 M.N.")]
    [InlineData("1000000000", "MIL MILLONES DE PESOS 00/100 M.N.")]
    [InlineData("1000000001", "MIL MILLONES UN PESOS 00/100 M.N.")]
    [InlineData("1001000000", "MIL UN MILLONES DE PESOS 00/100 M.N.")]
    [InlineData("999000000000", "NOVECIENTOS NOVENTA Y NUEVE MIL MILLONES DE PESOS 00/100 M.N.")]
    [InlineData("1000000000000", "UN BILLÓN DE PESOS 00/100 M.N.")]
    [InlineData("2000000000000", "DOS BILLONES DE PESOS 00/100 M.N.")]
    [InlineData("1000000000001", "UN BILLÓN UN PESOS 00/100 M.N.")]
    [InlineData("1000001000000", "UN BILLÓN UN MILLÓN DE PESOS 00/100 M.N.")]
    public void Escribe_los_millones_y_los_billones(string importe, string esperado) =>
        Assert.Equal(esperado, ImporteConLetra.De(D(importe), "MXN"));

    [Fact]
    public void Escribe_el_mayor_importe_que_cabe_en_un_CFDI()
    {
        var texto = ImporteConLetra.De(D("999999999999999999.99"), "MXN");
        Assert.StartsWith("NOVECIENTOS NOVENTA Y NUEVE MIL NOVECIENTOS NOVENTA Y NUEVE BILLONES", texto, StringComparison.Ordinal);
        Assert.EndsWith("NOVECIENTOS NOVENTA Y NUEVE MIL NOVECIENTOS NOVENTA Y NUEVE PESOS 99/100 M.N.", texto, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0.5", "CERO PESOS 50/100 M.N.")]
    [InlineData("0.01", "CERO PESOS 01/100 M.N.")]
    [InlineData("1.99", "UN PESO 99/100 M.N.")]
    [InlineData("1.994", "UN PESO 99/100 M.N.")] // 1.994 todavía cabe en 99 centavos
    [InlineData("1.995", "DOS PESOS 00/100 M.N.")] // el cinco sube y los centavos acarrean al peso
    [InlineData("0.995", "UN PESO 00/100 M.N.")]
    [InlineData("1.005", "UN PESO 01/100 M.N.")] // no es el redondeo bancario
    [InlineData("1.004999", "UN PESO 00/100 M.N.")]
    [InlineData("2.675", "DOS PESOS 68/100 M.N.")]
    public void Redondea_los_centavos_subiendo_el_cinco(string importe, string esperado) =>
        Assert.Equal(esperado, ImporteConLetra.De(D(importe), "MXN"));

    [Theory]
    [InlineData("1", "USD", "UN DÓLAR 00/100 USD")]
    [InlineData("1234.56", "USD", "MIL DOSCIENTOS TREINTA Y CUATRO DÓLARES 56/100 USD")]
    [InlineData("1000000", "USD", "UN MILLÓN DE DÓLARES 00/100 USD")]
    [InlineData("0", "USD", "CERO DÓLARES 00/100 USD")]
    [InlineData("1", "EUR", "UN EURO 00/100 EUR")]
    [InlineData("2", "EUR", "DOS EUROS 00/100 EUR")]
    [InlineData("2000000", "EUR", "DOS MILLONES DE EUROS 00/100 EUR")]
    [InlineData("5000", "GBP", "CINCO MIL 00/100 GBP")] // sin nombre conocido: el número, los centavos y la clave
    [InlineData("1000000", "JPY", "UN MILLÓN 00/100 JPY")]
    [InlineData("12.5", "mxn", "DOCE PESOS 50/100 M.N.")] // la clave se reconoce sin importar las mayúsculas
    public void Con_otras_monedas_usa_su_nombre_y_su_clave(string importe, string moneda, string esperado) =>
        Assert.Equal(esperado, ImporteConLetra.De(D(importe), moneda));

    [Fact]
    public void Un_importe_con_mas_de_dos_decimales_se_redondea_a_centavos()
    {
        // El total de un CFDI de otra moneda puede traer hasta seis decimales.
        Assert.Equal("UN DÓLAR 23/100 USD", ImporteConLetra.De(D("1.234567"), "USD"));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-0.01")]
    [InlineData("1000000000000000000")]
    [InlineData("999999999999999999.995")] // al redondear llega al tope
    public void Rechaza_lo_que_no_se_puede_escribir(string importe) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ImporteConLetra.De(D(importe), "MXN"));

    [Fact]
    public void Cada_entero_hasta_veinte_mil_sale_en_mayusculas_y_sin_espacios_dobles()
    {
        for (var n = 0; n <= 20_000; n++)
        {
            var texto = ImporteConLetra.De(n, "MXN");
            Assert.Equal(texto.ToUpperInvariant(), texto);
            Assert.DoesNotContain("  ", texto, StringComparison.Ordinal);
            Assert.False(texto.StartsWith(' ') || texto.EndsWith(' '), $"Espacios sobrantes en {n}: \"{texto}\"");
            Assert.EndsWith(" 00/100 M.N.", texto, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Los_numeros_del_cero_al_novecientos_noventa_y_nueve_no_repiten_texto()
    {
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        for (var n = 0; n <= 999; n++)
        {
            Assert.True(vistos.Add(ImporteConLetra.Numero(n)), $"El {n} sale igual que otro número.");
        }
    }
}
