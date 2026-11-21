using Comprobante.Core.Solicitud;
using static Comprobante.UnitTests.Datos;

namespace Comprobante.UnitTests.Solicitudes;

public class SolicitudCfdiTests
{
    [Fact]
    public void Normalizada_pone_los_valores_que_se_omitieron()
    {
        var n = new SolicitudCfdi { Conceptos = [Concepto(1, 10m), Concepto(1, 10m, traslados: [Iva()])] }.Normalizada();

        Assert.Equal("MXN", n.Moneda);
        Assert.Equal("I", n.TipoDeComprobante);
        Assert.Equal("01", n.Exportacion);
        Assert.Equal("01", n.Conceptos![0].ObjetoImp); // sin impuestos
        Assert.Equal("02", n.Conceptos![1].ObjetoImp); // con impuestos
    }

    [Fact]
    public void Normalizada_limpia_el_texto_y_pone_en_mayusculas_lo_que_lo_pide()
    {
        var n = new SolicitudCfdi
        {
            Moneda = " usd ",
            TipoDeComprobante = "e",
            MetodoPago = "pue",
            Serie = "   ",
            Emisor = new SolicitudEmisor { Rfc = " eku9003173c9 ", Nombre = " ESCUELA KEMPER URGATE " },
            Receptor = new SolicitudReceptor { Rfc = "xaxx010101000", UsoCfdi = "s01", ResidenciaFiscal = "usa" },
            Conceptos = [Concepto(1, 10m) with { ClaveUnidad = "e48 ", Traslados = [Iva() with { TipoFactor = "tasa" }] }],
        }.Normalizada();

        Assert.Equal("USD", n.Moneda);
        Assert.Equal("E", n.TipoDeComprobante);
        Assert.Equal("PUE", n.MetodoPago);
        Assert.Null(n.Serie);
        Assert.Equal("EKU9003173C9", n.Emisor!.Rfc);
        Assert.Equal("ESCUELA KEMPER URGATE", n.Emisor.Nombre);
        Assert.Equal("XAXX010101000", n.Receptor!.Rfc);
        Assert.Equal("S01", n.Receptor.UsoCfdi);
        Assert.Equal("USA", n.Receptor.ResidenciaFiscal);
        Assert.Equal("E48", n.Conceptos![0].ClaveUnidad);
        Assert.Equal("Tasa", n.Conceptos[0].Traslados![0].TipoFactor);
    }

    [Fact]
    public void Un_descuento_o_una_base_en_cero_se_toman_como_omitidos()
    {
        var concepto = Concepto(1, 10m, descuento: 0m, traslados: [Iva() with { Base = 0m }]);
        var n = Solicitud(concepto).Normalizada();

        Assert.Null(n.Conceptos![0].Descuento);
        Assert.Null(n.Conceptos[0].Traslados![0].Base);
    }
}
