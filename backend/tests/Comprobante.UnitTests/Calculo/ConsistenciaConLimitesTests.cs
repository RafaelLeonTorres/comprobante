using Comprobante.Core;
using Comprobante.Core.Calculo;
using Comprobante.Core.Solicitud;
using static Comprobante.UnitTests.Datos;

namespace Comprobante.UnitTests.Calculo;

/// <summary>
/// Lo que calcula la app debe cumplir siempre los límites que aplica el SAT. Con números al azar (la semilla es fija, así
/// que la prueba es repetible) se calcula un concepto con descuento e impuestos, se escribe como saldría en el XML y se
/// revisa su importe y el de cada impuesto contra <see cref="Limites"/>, con los decimales con que quedaron escritos.
/// </summary>
[Collection(ColeccionCatalogos.Nombre)]
public class ConsistenciaConLimitesTests(CatalogosFixture catalogos)
{
    private const int Decimales = 2;

    private static decimal Aleatorio(Random azar, long tope)
    {
        var mantisa = azar.NextInt64(1, tope);
        return new decimal((int)(mantisa & 0xFFFFFFFF), (int)(mantisa >> 32), 0, false, (byte)azar.Next(0, 7));
    }

    [Fact]
    public void Lo_que_calcula_la_app_siempre_cae_dentro_de_los_limites()
    {
        var azar = new Random(20261121);
        decimal[] tasas = [0m, 0.08m, 0.16m, 0.265m, 0.304m, 0.5m, 0.53m, 1.6m];
        var evaluados = 0;

        for (var caso = 0; caso < 3000; caso++)
        {
            var cantidad = Aleatorio(azar, 100_000_000);
            var valorUnitario = Aleatorio(azar, 100_000_000);
            var tasa = tasas[azar.Next(tasas.Length)];

            var bruto = Redondeo.Redondear(cantidad * valorUnitario, 6);
            decimal? descuento = azar.Next(3) == 0 ? Redondeo.Redondear(bruto * (decimal)azar.NextDouble() * 0.5m, azar.Next(2, 7)) : null;
            if (descuento == 0m)
            {
                descuento = null;
            }

            var solicitud = Solicitud(Concepto(cantidad, valorUnitario, descuento, traslados: [Iva(tasa)], retenciones: [RetencionIsr(0.10m)])).Normalizada();
            if (ValidadorSolicitud.Validar(solicitud, catalogos.Pequenos).Count > 0)
            {
                continue; // por ejemplo, un importe demasiado chico para tener base
            }

            evaluados++;
            var concepto = Calculador.Calcular(solicitud, Decimales).Conceptos.Single();

            var cantidadEscrita = Formato.Importe(cantidad, 0);
            var valorEscrito = Formato.Importe(valorUnitario, Decimales);
            var importeEscrito = Formato.Importe(concepto.Importe, Decimales);
            var rangoImporte = Limites.ImporteConcepto(
                D(cantidadEscrita), Redondeo.DecimalesEscritos(cantidadEscrita),
                D(valorEscrito), Redondeo.DecimalesEscritos(valorEscrito),
                Redondeo.DecimalesEscritos(importeEscrito))!.Value;
            Assert.True(
                rangoImporte.Contiene(D(importeEscrito)),
                $"Importe {importeEscrito} fuera de [{rangoImporte.Inferior}, {rangoImporte.Superior}] con {cantidadEscrita} x {valorEscrito}");

            foreach (var impuesto in concepto.Traslados.Concat(concepto.Retenciones))
            {
                var baseEscrita = Formato.Importe(impuesto.Base, Decimales);
                var tasaEscrita = Formato.Fijo(impuesto.TasaOCuota!.Value, 6);
                var importeImpuestoEscrito = Formato.Importe(impuesto.Importe!.Value, Decimales);
                var rango = Limites.ImporteImpuesto(
                    D(baseEscrita), Redondeo.DecimalesEscritos(baseEscrita), D(tasaEscrita), Redondeo.DecimalesEscritos(importeImpuestoEscrito))!.Value;
                Assert.True(
                    rango.Contiene(D(importeImpuestoEscrito)),
                    $"Impuesto {importeImpuestoEscrito} fuera de [{rango.Inferior}, {rango.Superior}] con base {baseEscrita} y tasa {tasaEscrita}");
            }
        }

        Assert.True(evaluados > 2500, $"Solo se evaluaron {evaluados} casos de 3000");
    }
}
