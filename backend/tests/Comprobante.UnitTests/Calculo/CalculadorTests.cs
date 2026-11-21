using Comprobante.Core.Calculo;
using Comprobante.Core.Solicitud;
using static Comprobante.UnitTests.Datos;

namespace Comprobante.UnitTests.Calculo;

[Collection(ColeccionCatalogos.Nombre)]
public class CalculadorTests(CatalogosFixture catalogos)
{
    private ResultadoCalculo Calcular(SolicitudCfdi solicitud)
    {
        var normalizada = solicitud.Normalizada();
        var errores = ValidadorSolicitud.Validar(normalizada, catalogos.Pequenos);
        Assert.Empty(errores);
        return Calculador.Calcular(normalizada, catalogos.Pequenos.Monedas[normalizada.Moneda!].Decimales);
    }

    [Fact]
    public void Un_concepto_con_iva()
    {
        var r = Calcular(Solicitud(Concepto(1, 100.00m, traslados: [Iva()])));

        Assert.Equal(100.00m, r.SubTotal);
        Assert.Null(r.Descuento);
        var iva = Assert.Single(r.Traslados);
        Assert.Equal(new TrasladoResumen(100.00m, "002", "Tasa", 0.16m, 16.00m), iva);
        Assert.Equal(16.00m, r.TotalImpuestosTrasladados);
        Assert.Null(r.TotalImpuestosRetenidos);
        Assert.Equal(116.00m, r.Total);
    }

    [Fact]
    public void El_redondeo_va_al_final_y_no_en_cada_concepto()
    {
        // Tres conceptos de 33.333333: redondeando cada uno a centavos sumarían 99.99; la regla del SAT da 100.00.
        var concepto = Concepto(1, 33.333333m, traslados: [Iva()]);
        var r = Calcular(Solicitud(concepto, concepto, concepto));

        Assert.All(r.Conceptos, c =>
        {
            Assert.Equal(33.333333m, c.Importe);
            Assert.Equal(5.333333m, Assert.Single(c.Traslados).Importe); // 33.333333 x 0.16 = 5.33333328, a seis decimales
        });
        Assert.Equal(100.00m, r.SubTotal); // 99.999999
        Assert.Equal(new TrasladoResumen(100.00m, "002", "Tasa", 0.16m, 16.00m), Assert.Single(r.Traslados)); // 15.999999
        Assert.Equal(116.00m, r.Total);
    }

    [Fact]
    public void Cantidad_con_seis_decimales_y_descuento_como_en_la_guia()
    {
        // Guía de llenado: 3.141649 x 1230.00 = 3864.22827 con descuento de 864.10.
        var r = Calcular(Solicitud(Concepto(3.141649m, 1230.00m, descuento: 864.10m, traslados: [Iva()])));

        var concepto = Assert.Single(r.Conceptos);
        Assert.Equal(3864.22827m, concepto.Importe);
        var iva = Assert.Single(concepto.Traslados);
        Assert.Equal(3000.12827m, iva.Base); // importe menos descuento
        Assert.Equal(480.020523m, iva.Importe); // 3000.12827 x 0.16 = 480.0205232

        Assert.Equal(3864.23m, r.SubTotal);
        Assert.Equal(864.10m, r.Descuento);
        Assert.Equal(new TrasladoResumen(3000.13m, "002", "Tasa", 0.16m, 480.02m), Assert.Single(r.Traslados));
        Assert.Equal(3480.15m, r.Total); // 3864.23 - 864.10 + 480.02
    }

    [Fact]
    public void Honorarios_con_retenciones_de_isr_y_de_iva()
    {
        var r = Calcular(Solicitud(Concepto(
            1, 15000.00m,
            traslados: [Iva()],
            retenciones: [RetencionIsr(0.10m), RetencionIva(0.106667m)])));

        Assert.Equal(15000.00m, r.SubTotal);
        Assert.Equal(2400.00m, Assert.Single(r.Traslados).Importe);
        Assert.Collection(
            r.Retenciones,
            isr => Assert.Equal(new RetencionResumen("001", 1500.00m), isr),
            iva => Assert.Equal(new RetencionResumen("002", 1600.01m), iva)); // 1600.005 sube al redondear
        Assert.Equal(2400.00m, r.TotalImpuestosTrasladados);
        Assert.Equal(3100.01m, r.TotalImpuestosRetenidos);
        Assert.Equal(14299.99m, r.Total); // 15000 + 2400 - 1500 - 1600.01
    }

    [Fact]
    public void Un_concepto_exento_no_lleva_importe_ni_total_de_traslados()
    {
        var r = Calcular(Solicitud(Concepto(1, 500.00m, traslados: [IvaExento()])));

        var exento = Assert.Single(r.Traslados);
        Assert.Equal(new TrasladoResumen(500.00m, "002", "Exento", null, null), exento);
        Assert.Null(r.TotalImpuestosTrasladados);
        Assert.Equal(500.00m, r.Total);
    }

    [Fact]
    public void Tasa_cero_si_lleva_importe_en_cero_y_total_de_traslados()
    {
        var r = Calcular(Solicitud(Concepto(2, 100.00m, traslados: [Iva(0m)])));

        Assert.Equal(new TrasladoResumen(200.00m, "002", "Tasa", 0m, 0.00m), Assert.Single(r.Traslados));
        Assert.Equal(0.00m, r.TotalImpuestosTrasladados);
        Assert.Equal(200.00m, r.Total);
    }

    [Fact]
    public void Ieps_por_cuota_usa_la_cantidad_de_unidades_como_base()
    {
        // 40 litros a 22.50: el IEPS por cuota se cobra por litro, no sobre el importe.
        var r = Calcular(Solicitud(Concepto(40, 22.50m, traslados: [IepsCuota(6.17m), Iva()])));

        var concepto = Assert.Single(r.Conceptos);
        var ieps = concepto.Traslados.Single(t => t.Impuesto == "003");
        Assert.Equal(40m, ieps.Base);
        Assert.Equal(246.80m, ieps.Importe);
        Assert.Equal(900.00m, concepto.Traslados.Single(t => t.Impuesto == "002").Base);

        Assert.Equal(900.00m, r.SubTotal);
        Assert.Equal(390.80m, r.TotalImpuestosTrasladados); // 246.80 + 144.00
        Assert.Equal(1290.80m, r.Total);
    }

    [Fact]
    public void La_base_indicada_en_la_solicitud_manda_sobre_la_habitual()
    {
        var iva = Iva() with { Base = 50.00m };
        var r = Calcular(Solicitud(Concepto(1, 100.00m, traslados: [iva])));

        Assert.Equal(new TrasladoResumen(50.00m, "002", "Tasa", 0.16m, 8.00m), Assert.Single(r.Traslados));
        Assert.Equal(108.00m, r.Total);
    }

    [Fact]
    public void El_resumen_agrupa_por_impuesto_factor_y_tasa_en_un_orden_fijo()
    {
        var r = Calcular(Solicitud(
            Concepto(1, 100.00m, traslados: [Iva(0.16m), Ieps(0.08m)]),
            Concepto(1, 50.00m, traslados: [IvaExento()]),
            Concepto(1, 200.00m, traslados: [Iva(0m)]),
            Concepto(2, 25.00m, traslados: [Iva(0.16m)])));

        Assert.Collection(
            r.Traslados,
            t => Assert.Equal(new TrasladoResumen(200.00m, "002", "Tasa", 0m, 0.00m), t),
            t => Assert.Equal(new TrasladoResumen(150.00m, "002", "Tasa", 0.16m, 24.00m), t),
            t => Assert.Equal(new TrasladoResumen(50.00m, "002", "Exento", null, null), t),
            t => Assert.Equal(new TrasladoResumen(100.00m, "003", "Tasa", 0.08m, 8.00m), t));
        Assert.Equal(32.00m, r.TotalImpuestosTrasladados);
        Assert.Equal(400.00m + 32.00m, r.Total);
    }

    [Fact]
    public void El_descuento_del_comprobante_solo_existe_si_algun_concepto_lo_lleva()
    {
        var sin = Calcular(Solicitud(Concepto(1, 100.00m)));
        var con = Calcular(Solicitud(Concepto(1, 100.00m), Concepto(1, 10.00m, descuento: 2.50m)));

        Assert.Null(sin.Descuento);
        Assert.Equal(2.50m, con.Descuento);
        Assert.Equal(107.50m, con.Total);
    }

    [Fact]
    public void Un_comprobante_sin_impuestos_no_trae_resumen()
    {
        var r = Calcular(Solicitud(Concepto(3, 10.00m)));

        Assert.Empty(r.Traslados);
        Assert.Empty(r.Retenciones);
        Assert.Null(r.TotalImpuestosTrasladados);
        Assert.Null(r.TotalImpuestosRetenidos);
        Assert.Equal(30.00m, r.Total);
    }

    [Fact]
    public void El_yen_no_tiene_decimales()
    {
        var r = Calcular(EnMoneda("JPY", Concepto(3, 1234.56m, traslados: [Iva()])));

        Assert.Equal(0, r.DecimalesMoneda);
        Assert.Equal(3703.68m, r.Conceptos[0].Importe); // el concepto conserva sus decimales
        Assert.Equal(3704m, r.SubTotal);
        Assert.Equal(new TrasladoResumen(3704m, "002", "Tasa", 0.16m, 593m), Assert.Single(r.Traslados)); // 592.5888
        Assert.Equal(4297m, r.Total);
    }

    [Fact]
    public void Una_moneda_de_tres_decimales_redondea_a_tres()
    {
        // El ejemplo del Anexo 20 con moneda OMR: 281.594680 se redondea a 281.595.
        var r = Calcular(EnMoneda("OMR", Concepto(1, 281.594680m)));

        Assert.Equal(3, r.DecimalesMoneda);
        Assert.Equal(281.595m, r.SubTotal);
        Assert.Equal(281.595m, r.Total);
    }

    [Fact]
    public void Egreso_calcula_igual_que_ingreso()
    {
        var ingreso = Calcular(Solicitud(Concepto(2, 10.00m, traslados: [Iva()])));
        var egreso = Calcular(new SolicitudCfdi
        {
            TipoDeComprobante = "E",
            Conceptos = [Concepto(2, 10.00m, traslados: [Iva()])],
        });

        Assert.Equal(ingreso.Total, egreso.Total);
        Assert.Equal(23.20m, egreso.Total);
    }
}
