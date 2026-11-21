namespace Comprobante.Core.Documento;

/// <summary>
/// El comprobante tal como está en el XML: todos los valores son texto, exactamente como se escriben (o como se leyeron),
/// y nulo cuando el atributo no está. El generador lo arma y escribe; el validador lo lee y revisa sus reglas.
/// Los nombres siguen los atributos del Anexo 20.
/// </summary>
public sealed record Cfdi
{
    public string? Version { get; init; }
    public string? Serie { get; init; }
    public string? Folio { get; init; }
    public string? Fecha { get; init; }
    public string? Sello { get; init; }
    public string? FormaPago { get; init; }
    public string? NoCertificado { get; init; }
    public string? Certificado { get; init; }
    public string? CondicionesDePago { get; init; }
    public string? SubTotal { get; init; }
    public string? Descuento { get; init; }
    public string? Moneda { get; init; }
    public string? TipoCambio { get; init; }
    public string? Total { get; init; }
    public string? TipoDeComprobante { get; init; }
    public string? Exportacion { get; init; }
    public string? MetodoPago { get; init; }
    public string? LugarExpedicion { get; init; }
    public string? Confirmacion { get; init; }

    public CfdiInformacionGlobal? InformacionGlobal { get; init; }
    public IReadOnlyList<CfdiRelacionados> CfdiRelacionados { get; init; } = [];
    public CfdiEmisor? Emisor { get; init; }
    public CfdiReceptor? Receptor { get; init; }
    public IReadOnlyList<CfdiConcepto> Conceptos { get; init; } = [];

    /// <summary>El nodo de impuestos del comprobante; nulo si el XML no lo trae.</summary>
    public CfdiImpuestos? Impuestos { get; init; }
}

public sealed record CfdiInformacionGlobal
{
    public string? Periodicidad { get; init; }
    public string? Meses { get; init; }
    public string? Anio { get; init; }
}

public sealed record CfdiRelacionados
{
    public string? TipoRelacion { get; init; }
    public IReadOnlyList<string> Uuids { get; init; } = [];
}

public sealed record CfdiEmisor
{
    public string? Rfc { get; init; }
    public string? Nombre { get; init; }
    public string? RegimenFiscal { get; init; }
    public string? FacAtrAdquirente { get; init; }
}

public sealed record CfdiReceptor
{
    public string? Rfc { get; init; }
    public string? Nombre { get; init; }
    public string? DomicilioFiscalReceptor { get; init; }
    public string? ResidenciaFiscal { get; init; }
    public string? NumRegIdTrib { get; init; }
    public string? RegimenFiscalReceptor { get; init; }

    /// <summary>Atributo UsoCFDI.</summary>
    public string? UsoCfdi { get; init; }
}

public sealed record CfdiConcepto
{
    public string? ClaveProdServ { get; init; }
    public string? NoIdentificacion { get; init; }
    public string? Cantidad { get; init; }
    public string? ClaveUnidad { get; init; }
    public string? Unidad { get; init; }
    public string? Descripcion { get; init; }
    public string? ValorUnitario { get; init; }
    public string? Importe { get; init; }
    public string? Descuento { get; init; }
    public string? ObjetoImp { get; init; }

    /// <summary>Si el concepto trae el nodo cfdi:Impuestos (aunque no tenga traslados ni retenciones).</summary>
    public bool TieneImpuestos { get; init; }

    public IReadOnlyList<CfdiImpuesto> Traslados { get; init; } = [];
    public IReadOnlyList<CfdiImpuesto> Retenciones { get; init; } = [];
}

/// <summary>
/// Un traslado o una retención: de un concepto (con su base) o del resumen del comprobante. En el resumen, las retenciones
/// solo llevan Impuesto e Importe, y los traslados exentos solo Base, Impuesto y TipoFactor.
/// </summary>
public sealed record CfdiImpuesto
{
    public string? Base { get; init; }
    public string? Impuesto { get; init; }
    public string? TipoFactor { get; init; }
    public string? TasaOCuota { get; init; }
    public string? Importe { get; init; }
}

public sealed record CfdiImpuestos
{
    public string? TotalImpuestosRetenidos { get; init; }
    public string? TotalImpuestosTrasladados { get; init; }
    public IReadOnlyList<CfdiImpuesto> Retenciones { get; init; } = [];
    public IReadOnlyList<CfdiImpuesto> Traslados { get; init; } = [];
}
