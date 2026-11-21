using Comprobante.Core;
using Comprobante.Core.Solicitud;
using static Comprobante.UnitTests.Datos;

namespace Comprobante.UnitTests.Solicitudes;

[Collection(ColeccionCatalogos.Nombre)]
public class ValidadorSolicitudTests(CatalogosFixture catalogos)
{
    private IReadOnlyList<ErrorCampo> Errores(SolicitudCfdi solicitud) =>
        ValidadorSolicitud.Validar(solicitud.Normalizada(), catalogos.Pequenos);

    private void Falla(SolicitudCfdi solicitud, string campo, string fragmentoDelMensaje)
    {
        var error = Assert.Single(Errores(solicitud), e => e.Campo == campo);
        Assert.Contains(fragmentoDelMensaje, error.Mensaje);
    }

    [Fact]
    public void Una_solicitud_correcta_no_tiene_errores() =>
        Assert.Empty(Errores(Solicitud(Concepto(1, 100m, traslados: [Iva()], retenciones: [RetencionIsr(0.10m)]))));

    [Fact]
    public void Hace_falta_al_menos_un_concepto()
    {
        Falla(new SolicitudCfdi(), "conceptos", "al menos un concepto");
        Falla(new SolicitudCfdi { Conceptos = [] }, "conceptos", "al menos un concepto");
    }

    [Fact]
    public void Solo_se_arman_ingresos_y_egresos()
    {
        var solicitud = Solicitud(Concepto(1, 10m)) with { TipoDeComprobante = "T" };

        Falla(solicitud, "tipoDeComprobante", "ingreso (I) y de egreso (E)");
    }

    [Theory]
    [InlineData("0", "debe ser mayor que cero")]
    [InlineData("-1", "debe ser mayor que cero")]
    [InlineData("1.1234567", "hasta 6 decimales")]
    [InlineData("1000000000000", "menor que 1,000,000,000,000")]
    public void La_cantidad_y_el_valor_unitario_se_revisan(string valor, string mensaje)
    {
        Falla(Solicitud(Concepto(D(valor), 10m)), "conceptos[0].cantidad", mensaje);
        Falla(Solicitud(Concepto(1, D(valor))), "conceptos[0].valorUnitario", mensaje);
    }

    [Fact]
    public void El_importe_del_concepto_no_puede_ser_enorme()
    {
        var solicitud = Solicitud(Concepto(999_999_999_999m, 999_999_999_999m));

        Falla(solicitud, "conceptos[0].valorUnitario", "1,000,000,000,000,000");
    }

    [Fact]
    public void Los_errores_dicen_en_que_concepto_estan()
    {
        var errores = Errores(Solicitud(Concepto(1, 10m), Concepto(0, 10m), Concepto(1, 0m)));

        Assert.Contains(errores, e => e.Campo == "conceptos[1].cantidad");
        Assert.Contains(errores, e => e.Campo == "conceptos[2].valorUnitario");
        Assert.DoesNotContain(errores, e => e.Campo.StartsWith("conceptos[0]"));
    }

    [Fact]
    public void El_descuento_se_revisa()
    {
        Falla(Solicitud(Concepto(1, 10m, descuento: -1m)), "conceptos[0].descuento", "no puede ser negativo");
        Falla(Solicitud(Concepto(1, 10m, descuento: 0.1234567m)), "conceptos[0].descuento", "hasta 6 decimales");
        Falla(Solicitud(Concepto(2, 10m, descuento: 20.01m)), "conceptos[0].descuento", "no puede ser mayor que el importe");
        Assert.Empty(Errores(Solicitud(Concepto(2, 10m, descuento: 20m))));
    }

    [Fact]
    public void Con_descuento_igual_al_importe_los_impuestos_no_tienen_base()
    {
        Falla(
            Solicitud(Concepto(1, 10m, descuento: 10m, traslados: [Iva()])),
            "conceptos[0].descuento",
            "base de los impuestos queda en cero");
    }

    [Fact]
    public void La_moneda_debe_estar_en_el_catalogo()
    {
        Falla(Solicitud(Concepto(1, 10m)) with { Moneda = "ZZZ" }, "moneda", "no está en el catálogo c_Moneda");
    }

    [Theory]
    [InlineData("USD", null, "obligatorio")]
    [InlineData("USD", "0", "mayor que cero")]
    [InlineData("USD", "17.1234567", "hasta 6 decimales")]
    [InlineData("MXN", "2", "se omite o vale 1")]
    [InlineData("XXX", "1", "no se registra tipo de cambio")]
    public void El_tipo_de_cambio_depende_de_la_moneda(string moneda, string? tipoCambio, string mensaje)
    {
        var solicitud = Solicitud(Concepto(1, 10m)) with
        {
            Moneda = moneda,
            TipoCambio = tipoCambio is null ? null : D(tipoCambio),
        };

        Falla(solicitud, "tipoCambio", mensaje);
    }

    [Theory]
    [InlineData("MXN", null)]
    [InlineData("MXN", "1")]
    [InlineData("USD", "17.5")]
    [InlineData("XXX", null)]
    public void Tipos_de_cambio_correctos(string moneda, string? tipoCambio) =>
        Assert.Empty(Errores(Solicitud(Concepto(1, 10m)) with { Moneda = moneda, TipoCambio = tipoCambio is null ? null : D(tipoCambio) }));

    [Fact]
    public void El_objeto_de_impuesto_decide_si_el_concepto_lleva_impuestos()
    {
        var iva = new[] { Iva() };

        Falla(Solicitud(Concepto(1, 10m, traslados: iva) with { ObjetoImp = "01" }), "conceptos[0].objetoImp", "no lleva impuestos");
        Falla(Solicitud(Concepto(1, 10m) with { ObjetoImp = "02" }), "conceptos[0].traslados", "al menos un impuesto");
        Falla(Solicitud(Concepto(1, 10m) with { ObjetoImp = "99" }), "conceptos[0].objetoImp", "c_ObjetoImp");
        Assert.Empty(Errores(Solicitud(Concepto(1, 10m) with { ObjetoImp = "01" })));
        Assert.Empty(Errores(Solicitud(Concepto(1, 10m) with { ObjetoImp = "03" })));
    }

    [Fact]
    public void Los_impuestos_se_revisan_uno_por_uno()
    {
        var solicitud = Solicitud(Concepto(
            1, 10m,
            traslados:
            [
                new SolicitudImpuesto { Impuesto = "001", TipoFactor = "Tasa", TasaOCuota = 0.1m },
                new SolicitudImpuesto { Impuesto = "002", TipoFactor = "Tasa" },
                new SolicitudImpuesto { Impuesto = "002", TipoFactor = "Exento", TasaOCuota = 0.16m },
                new SolicitudImpuesto { Impuesto = "002", TipoFactor = "Otro" },
                new SolicitudImpuesto { Impuesto = "999", TipoFactor = "Tasa", TasaOCuota = 0.16m },
                new SolicitudImpuesto { Impuesto = "002", TipoFactor = "Tasa", TasaOCuota = -0.16m },
                new SolicitudImpuesto { Impuesto = "002", TipoFactor = "Tasa", TasaOCuota = 0.1234567m },
            ],
            retenciones: [new SolicitudImpuesto { Impuesto = "002", TipoFactor = "Exento" }]));

        var errores = Errores(solicitud).ToDictionary(e => e.Campo, e => e.Mensaje);

        Assert.Contains("no se traslada", errores["conceptos[0].traslados[0].impuesto"]);
        Assert.Contains("obligatoria", errores["conceptos[0].traslados[1].tasaOCuota"]);
        Assert.Contains("no se registra tasa ni cuota", errores["conceptos[0].traslados[2].tasaOCuota"]);
        Assert.Contains("Tasa, Cuota o Exento", errores["conceptos[0].traslados[3].tipoFactor"]);
        Assert.Contains("Elige el impuesto", errores["conceptos[0].traslados[4].impuesto"]);
        Assert.Contains("no puede ser negativa", errores["conceptos[0].traslados[5].tasaOCuota"]);
        Assert.Contains("hasta 6 decimales", errores["conceptos[0].traslados[6].tasaOCuota"]);
        Assert.Contains("no puede ser exenta", errores["conceptos[0].retenciones[0].tipoFactor"]);
    }

    [Fact]
    public void La_base_indicada_se_revisa()
    {
        Falla(Solicitud(Concepto(1, 10m, traslados: [Iva() with { Base = -1m }])), "conceptos[0].traslados[0].base", "mayor que cero");
        Falla(Solicitud(Concepto(1, 10m, traslados: [Iva() with { Base = 1.1234567m }])), "conceptos[0].traslados[0].base", "hasta 6 decimales");
    }
}
