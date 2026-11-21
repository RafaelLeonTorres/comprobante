using Comprobante.Core.Solicitud;

namespace Comprobante.Core.Calculo;

/// <summary>
/// Calcula importes, impuestos y totales con las reglas del Anexo 20 y su guía de llenado: cada concepto y cada impuesto
/// de concepto con hasta seis decimales y el redondeo al final, a los decimales de la moneda, sobre las sumas.
/// </summary>
public static class Calculador
{
    /// <summary>
    /// Calcula la solicitud, que debe estar normalizada y haber pasado <see cref="ValidadorSolicitud"/>.
    /// </summary>
    /// <param name="decimalesMoneda">Decimales de la moneda según c_Moneda (MXN y USD: 2; JPY: 0; BHD: 3).</param>
    public static ResultadoCalculo Calcular(SolicitudCfdi solicitud, int decimalesMoneda)
    {
        var conceptos = (solicitud.Conceptos ?? []).Select(CalcularConcepto).ToList();

        var subTotal = Redondeo.Redondear(conceptos.Sum(c => c.Importe), decimalesMoneda);
        decimal? descuento = conceptos.Any(c => c.Descuento is not null)
            ? Redondeo.Redondear(conceptos.Sum(c => c.Descuento ?? 0m), decimalesMoneda)
            : null;

        var traslados = conceptos
            .SelectMany(c => c.Traslados)
            .GroupBy(t => (t.Impuesto, t.TipoFactor, t.TasaOCuota))
            .Select(g => new TrasladoResumen(
                Redondeo.Redondear(g.Sum(t => t.Base), decimalesMoneda),
                g.Key.Impuesto,
                g.Key.TipoFactor,
                g.Key.TasaOCuota,
                g.Key.TipoFactor == "Exento"
                    ? null
                    : Redondeo.Redondear(g.Sum(t => t.Importe ?? 0m), decimalesMoneda)))
            .OrderBy(t => t.Impuesto, StringComparer.Ordinal)
            .ThenBy(t => OrdenDeFactor(t.TipoFactor))
            .ThenBy(t => t.TasaOCuota)
            .ToList();

        var retenciones = conceptos
            .SelectMany(c => c.Retenciones)
            .GroupBy(r => r.Impuesto)
            .Select(g => new RetencionResumen(
                g.Key, Redondeo.Redondear(g.Sum(r => r.Importe ?? 0m), decimalesMoneda)))
            .OrderBy(r => r.Impuesto, StringComparer.Ordinal)
            .ToList();

        // Los exentos no llevan importe: si solo hay exentos, el comprobante no trae TotalImpuestosTrasladados.
        decimal? totalTrasladados = traslados.Any(t => t.Importe is not null)
            ? traslados.Sum(t => t.Importe ?? 0m)
            : null;
        decimal? totalRetenidos = retenciones.Count > 0 ? retenciones.Sum(r => r.Importe) : null;

        var total = subTotal - (descuento ?? 0m) + (totalTrasladados ?? 0m) - (totalRetenidos ?? 0m);

        return new ResultadoCalculo(
            solicitud.Moneda ?? "MXN",
            decimalesMoneda,
            conceptos,
            subTotal,
            descuento,
            traslados,
            retenciones,
            totalTrasladados,
            totalRetenidos,
            total);
    }

    private static ConceptoCalculado CalcularConcepto(SolicitudConcepto concepto)
    {
        var importe = Redondeo.Redondear(concepto.Cantidad * concepto.ValorUnitario, Redondeo.DecimalesMaximos);
        var baseMonetaria = importe - (concepto.Descuento ?? 0m);

        return new ConceptoCalculado(
            importe,
            concepto.Descuento,
            (concepto.Traslados ?? []).Select(t => CalcularImpuesto(t, concepto, baseMonetaria)).ToList(),
            (concepto.Retenciones ?? []).Select(r => CalcularImpuesto(r, concepto, baseMonetaria)).ToList());
    }

    /// <summary>
    /// La base es la indicada en la solicitud o, si no, la habitual: el importe menos el descuento para Tasa y Exento, y
    /// la cantidad de unidades para Cuota (la cuota es un monto por unidad, no un porcentaje del importe).
    /// </summary>
    private static ImpuestoCalculado CalcularImpuesto(SolicitudImpuesto impuesto, SolicitudConcepto concepto, decimal baseMonetaria)
    {
        var baseImpuesto = impuesto.Base ?? (impuesto.TipoFactor == "Cuota" ? concepto.Cantidad : baseMonetaria);

        return impuesto.TipoFactor == "Exento"
            ? new ImpuestoCalculado(impuesto.Impuesto!, "Exento", null, baseImpuesto, null)
            : new ImpuestoCalculado(
                impuesto.Impuesto!,
                impuesto.TipoFactor!,
                impuesto.TasaOCuota,
                baseImpuesto,
                Redondeo.Redondear(baseImpuesto * impuesto.TasaOCuota!.Value, Redondeo.DecimalesMaximos));
    }

    private static int OrdenDeFactor(string factor) =>
        factor switch
        {
            "Tasa" => 0,
            "Cuota" => 1,
            _ => 2,
        };
}
