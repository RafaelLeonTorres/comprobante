using System.Globalization;
using Comprobante.Core.Calculo;
using Comprobante.Core.Documento;
using Comprobante.Core.Sello;
using Comprobante.Core.Solicitud;

namespace Comprobante.Core.Generacion;

/// <summary>
/// Arma el documento del CFDI (todo en texto, como va en el XML) con los datos de la solicitud y el resultado del cálculo.
/// Aquí se decide cómo se escribe cada número, que es lo que el SAT revisa:
/// los importes del comprobante y del resumen con los decimales de la moneda, los de cada concepto con esos decimales y los
/// que haga falta hasta seis, la tasa siempre con seis y la cantidad sin ceros de más.
/// </summary>
internal static class ConstructorCfdi
{
    /// <summary>La solicitud debe venir normalizada y el cálculo, hecho a partir de ella.</summary>
    public static Cfdi Construir(SolicitudCfdi solicitud, ResultadoCalculo calculo, Csd csd, string fecha)
    {
        var decimales = calculo.DecimalesMoneda;
        var conceptos = solicitud.Conceptos ?? [];

        return new Cfdi
        {
            Version = "4.0",
            Serie = solicitud.Serie,
            Folio = solicitud.Folio,
            Fecha = fecha,
            FormaPago = solicitud.FormaPago,
            NoCertificado = csd.NoCertificado,
            Certificado = csd.CertificadoBase64,
            CondicionesDePago = solicitud.CondicionesDePago,
            SubTotal = Formato.Fijo(calculo.SubTotal, decimales),
            Descuento = calculo.Descuento is { } descuento ? Formato.Fijo(descuento, decimales) : null,
            Moneda = solicitud.Moneda,
            TipoCambio = TipoCambio(solicitud),
            Total = Formato.Fijo(calculo.Total, decimales),
            TipoDeComprobante = solicitud.TipoDeComprobante,
            Exportacion = solicitud.Exportacion,
            MetodoPago = solicitud.MetodoPago,
            LugarExpedicion = solicitud.LugarExpedicion,
            InformacionGlobal = solicitud.InformacionGlobal is { } global
                ? new CfdiInformacionGlobal
                {
                    Periodicidad = global.Periodicidad,
                    Meses = global.Meses,
                    Anio = global.Anio.ToString(CultureInfo.InvariantCulture),
                }
                : null,
            CfdiRelacionados = (solicitud.CfdiRelacionados ?? [])
                .Select(r => new CfdiRelacionados { TipoRelacion = r.TipoRelacion, Uuids = r.Uuids ?? [] })
                .ToList(),
            Emisor = solicitud.Emisor is { } emisor
                ? new CfdiEmisor
                {
                    Rfc = emisor.Rfc,
                    Nombre = emisor.Nombre,
                    RegimenFiscal = emisor.RegimenFiscal,
                    FacAtrAdquirente = emisor.FacAtrAdquirente,
                }
                : null,
            Receptor = solicitud.Receptor is { } receptor
                ? new CfdiReceptor
                {
                    Rfc = receptor.Rfc,
                    Nombre = receptor.Nombre,
                    DomicilioFiscalReceptor = receptor.DomicilioFiscalReceptor,
                    ResidenciaFiscal = receptor.ResidenciaFiscal,
                    NumRegIdTrib = receptor.NumRegIdTrib,
                    RegimenFiscalReceptor = receptor.RegimenFiscalReceptor,
                    UsoCfdi = receptor.UsoCfdi,
                }
                : null,
            Conceptos = conceptos.Select((c, i) => Concepto(c, calculo.Conceptos[i], decimales)).ToList(),
            Impuestos = Resumen(calculo, decimales),
        };
    }

    /// <summary>MXN y XXX no llevan tipo de cambio; con otra moneda va con sus decimales y sin ceros de más.</summary>
    private static string? TipoCambio(SolicitudCfdi solicitud) =>
        solicitud.Moneda is "MXN" or "XXX" || solicitud.TipoCambio is not { } tipoCambio
            ? null
            : Formato.Importe(tipoCambio, 0);

    private static CfdiConcepto Concepto(SolicitudConcepto solicitud, ConceptoCalculado calculado, int decimales)
    {
        // El Importe se escribe al menos con los decimales del Descuento: el SAT revisa que el descuento no tenga más.
        var decimalesDelImporte = Math.Max(decimales, Redondeo.Decimales(calculado.Descuento ?? 0m));
        return new CfdiConcepto
        {
            ClaveProdServ = solicitud.ClaveProdServ,
            NoIdentificacion = solicitud.NoIdentificacion,
            Cantidad = Formato.Importe(solicitud.Cantidad, 0),
            ClaveUnidad = solicitud.ClaveUnidad,
            Unidad = solicitud.Unidad,
            Descripcion = solicitud.Descripcion,
            ValorUnitario = Formato.Importe(solicitud.ValorUnitario, decimales),
            Importe = Formato.Importe(calculado.Importe, decimalesDelImporte),
            Descuento = calculado.Descuento is { } descuento ? Formato.Importe(descuento, decimales) : null,
            ObjetoImp = solicitud.ObjetoImp,
            TieneImpuestos = calculado.Traslados.Count + calculado.Retenciones.Count > 0,
            Traslados = calculado.Traslados.Select(t => ImpuestoDelConcepto(t, decimales)).ToList(),
            Retenciones = calculado.Retenciones.Select(r => ImpuestoDelConcepto(r, decimales)).ToList(),
        };
    }

    private static CfdiImpuesto ImpuestoDelConcepto(ImpuestoCalculado impuesto, int decimales) => new()
    {
        Base = Formato.Importe(impuesto.Base, decimales),
        Impuesto = impuesto.Impuesto,
        TipoFactor = impuesto.TipoFactor,
        TasaOCuota = impuesto.TasaOCuota is { } tasa ? Formato.Fijo(tasa, 6) : null,
        Importe = impuesto.Importe is { } importe ? Formato.Importe(importe, decimales) : null,
    };

    /// <summary>El resumen del comprobante: con exactamente los decimales de la moneda, sin más ni menos.</summary>
    private static CfdiImpuestos? Resumen(ResultadoCalculo calculo, int decimales)
    {
        if (calculo.Traslados.Count + calculo.Retenciones.Count == 0)
        {
            return null;
        }

        return new CfdiImpuestos
        {
            TotalImpuestosRetenidos = calculo.TotalImpuestosRetenidos is { } retenidos ? Formato.Fijo(retenidos, decimales) : null,
            TotalImpuestosTrasladados = calculo.TotalImpuestosTrasladados is { } trasladados ? Formato.Fijo(trasladados, decimales) : null,
            Retenciones = calculo.Retenciones
                .Select(r => new CfdiImpuesto { Impuesto = r.Impuesto, Importe = Formato.Fijo(r.Importe, decimales) })
                .ToList(),
            Traslados = calculo.Traslados
                .Select(t => new CfdiImpuesto
                {
                    Base = Formato.Fijo(t.Base, decimales),
                    Impuesto = t.Impuesto,
                    TipoFactor = t.TipoFactor,
                    TasaOCuota = t.TasaOCuota is { } tasa ? Formato.Fijo(tasa, 6) : null,
                    Importe = t.Importe is { } importe ? Formato.Fijo(importe, decimales) : null,
                })
                .ToList(),
        };
    }
}
