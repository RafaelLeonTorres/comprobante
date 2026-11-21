namespace Comprobante.Core.Calculo;

/// <summary>
/// Importes, impuestos y totales de un comprobante. Lo de cada concepto lleva hasta seis decimales; el subtotal, el
/// descuento, el resumen de impuestos y el total ya van redondeados a los decimales de la moneda.
/// </summary>
public sealed record ResultadoCalculo(
    string Moneda,
    int DecimalesMoneda,
    IReadOnlyList<ConceptoCalculado> Conceptos,
    decimal SubTotal,
    decimal? Descuento,
    IReadOnlyList<TrasladoResumen> Traslados,
    IReadOnlyList<RetencionResumen> Retenciones,
    decimal? TotalImpuestosTrasladados,
    decimal? TotalImpuestosRetenidos,
    decimal Total);

/// <summary>Un concepto ya calculado, en el mismo orden que en la solicitud.</summary>
public sealed record ConceptoCalculado(
    decimal Importe,
    decimal? Descuento,
    IReadOnlyList<ImpuestoCalculado> Traslados,
    IReadOnlyList<ImpuestoCalculado> Retenciones);

/// <summary>Un traslado o una retención de un concepto: <see cref="Base"/> es el importe menos el descuento.</summary>
public sealed record ImpuestoCalculado(
    string Impuesto, string TipoFactor, decimal? TasaOCuota, decimal Base, decimal? Importe);

/// <summary>Una fila del resumen de impuestos trasladados: un renglón por impuesto, factor y tasa o cuota.</summary>
public sealed record TrasladoResumen(
    decimal Base, string Impuesto, string TipoFactor, decimal? TasaOCuota, decimal? Importe);

/// <summary>Una fila del resumen de impuestos retenidos: un renglón por impuesto.</summary>
public sealed record RetencionResumen(string Impuesto, decimal Importe);
