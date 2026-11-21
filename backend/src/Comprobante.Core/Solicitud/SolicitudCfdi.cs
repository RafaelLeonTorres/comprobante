namespace Comprobante.Core.Solicitud;

/// <summary>
/// Los datos con los que se arma un CFDI 4.0 de ingreso o de egreso. Es lo que llega en el JSON de la API y lo que
/// recibe la librería; los importes y los impuestos no se escriben aquí: los calcula <c>Calculador</c>.
/// Los nombres siguen los atributos del XML (Anexo 20), en la forma habitual de JSON (primera letra en minúscula).
/// </summary>
public sealed record SolicitudCfdi
{
    /// <summary>Atributo Serie: hasta 25 caracteres, opcional.</summary>
    public string? Serie { get; init; }

    /// <summary>Atributo Folio: hasta 40 caracteres, opcional.</summary>
    public string? Folio { get; init; }

    /// <summary>
    /// Fecha y hora de expedición, en la hora local del lugar de expedición y sin zona horaria (<c>2026-11-21T09:30:00</c>):
    /// se escribe tal cual, sin fracciones de segundo. Si se omite se usa la hora actual de ese lugar (la zona la da el
    /// catálogo de códigos postales).
    /// </summary>
    public DateTime? Fecha { get; init; }

    /// <summary>Clave de c_FormaPago (01 Efectivo, 03 Transferencia...). Con método PPD es 99.</summary>
    public string? FormaPago { get; init; }

    /// <summary>PUE (una sola exhibición) o PPD (parcialidades o diferido).</summary>
    public string? MetodoPago { get; init; }

    public string? CondicionesDePago { get; init; }

    /// <summary>Clave de c_Moneda; si se omite, MXN.</summary>
    public string? Moneda { get; init; }

    /// <summary>Tipo de cambio FIX: obligatorio si la moneda no es MXN ni XXX.</summary>
    public decimal? TipoCambio { get; init; }

    /// <summary>I (ingreso) o E (egreso); si se omite, I.</summary>
    public string? TipoDeComprobante { get; init; }

    /// <summary>Clave de c_Exportacion; si se omite, 01 (no aplica).</summary>
    public string? Exportacion { get; init; }

    /// <summary>Código postal del lugar de expedición.</summary>
    public string? LugarExpedicion { get; init; }

    public SolicitudEmisor? Emisor { get; init; }

    public SolicitudReceptor? Receptor { get; init; }

    /// <summary>Obligatoria cuando el receptor es el público en general (RFC genérico nacional y nombre "PUBLICO EN GENERAL").</summary>
    public SolicitudInformacionGlobal? InformacionGlobal { get; init; }

    public IReadOnlyList<SolicitudCfdiRelacionados>? CfdiRelacionados { get; init; }

    public IReadOnlyList<SolicitudConcepto>? Conceptos { get; init; }

    /// <summary>
    /// La solicitud con los valores que se omitieron ya puestos (moneda MXN, tipo I, exportación 01, objeto de impuesto
    /// según lleve o no impuestos), el texto sin espacios de sobra y los RFC en mayúsculas.
    /// </summary>
    public SolicitudCfdi Normalizada() => this with
    {
        Serie = Limpio(Serie),
        Folio = Limpio(Folio),
        FormaPago = Limpio(FormaPago),
        MetodoPago = Limpio(MetodoPago)?.ToUpperInvariant(),
        CondicionesDePago = Limpio(CondicionesDePago),
        Moneda = (Limpio(Moneda) ?? "MXN").ToUpperInvariant(),
        TipoDeComprobante = (Limpio(TipoDeComprobante) ?? "I").ToUpperInvariant(),
        Exportacion = Limpio(Exportacion) ?? "01",
        LugarExpedicion = Limpio(LugarExpedicion),
        Emisor = Emisor is null
            ? null
            : Emisor with
            {
                Rfc = Limpio(Emisor.Rfc)?.ToUpperInvariant(),
                Nombre = Limpio(Emisor.Nombre),
                RegimenFiscal = Limpio(Emisor.RegimenFiscal),
                FacAtrAdquirente = Limpio(Emisor.FacAtrAdquirente),
            },
        Receptor = Receptor is null
            ? null
            : Receptor with
            {
                Rfc = Limpio(Receptor.Rfc)?.ToUpperInvariant(),
                Nombre = Limpio(Receptor.Nombre),
                DomicilioFiscalReceptor = Limpio(Receptor.DomicilioFiscalReceptor),
                ResidenciaFiscal = Limpio(Receptor.ResidenciaFiscal)?.ToUpperInvariant(),
                NumRegIdTrib = Limpio(Receptor.NumRegIdTrib),
                RegimenFiscalReceptor = Limpio(Receptor.RegimenFiscalReceptor),
                UsoCfdi = Limpio(Receptor.UsoCfdi)?.ToUpperInvariant(),
            },
        InformacionGlobal = InformacionGlobal is null
            ? null
            : InformacionGlobal with
            {
                Periodicidad = Limpio(InformacionGlobal.Periodicidad),
                Meses = Limpio(InformacionGlobal.Meses),
            },
        CfdiRelacionados = CfdiRelacionados?
            .Select(r => r with
            {
                TipoRelacion = Limpio(r.TipoRelacion),
                Uuids = (r.Uuids ?? []).Select(u => u.Trim().ToUpperInvariant()).ToList(),
            })
            .ToList(),
        Conceptos = Conceptos?.Select(c => c.Normalizado()).ToList(),
    };

    internal static string? Limpio(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

public sealed record SolicitudEmisor
{
    /// <summary>RFC del emisor. En esta app debe ser el de uno de los certificados de prueba del SAT.</summary>
    public string? Rfc { get; init; }

    /// <summary>Nombre o razón social, sin el régimen societario ("S.A. de C.V.").</summary>
    public string? Nombre { get; init; }

    /// <summary>Clave de c_RegimenFiscal que corresponda al tipo de persona (12 posiciones moral, 13 física).</summary>
    public string? RegimenFiscal { get; init; }

    public string? FacAtrAdquirente { get; init; }
}

public sealed record SolicitudReceptor
{
    public string? Rfc { get; init; }

    public string? Nombre { get; init; }

    /// <summary>Código postal del domicilio fiscal del receptor (con RFC genérico, el del lugar de expedición).</summary>
    public string? DomicilioFiscalReceptor { get; init; }

    /// <summary>Clave de c_Pais: solo para receptores extranjeros.</summary>
    public string? ResidenciaFiscal { get; init; }

    public string? NumRegIdTrib { get; init; }

    public string? RegimenFiscalReceptor { get; init; }

    /// <summary>Clave de c_UsoCFDI (G01, G03, S01...).</summary>
    public string? UsoCfdi { get; init; }
}

public sealed record SolicitudInformacionGlobal
{
    /// <summary>Clave de c_Periodicidad (01 Diario, 02 Semanal, 03 Quincenal, 04 Mensual, 05 Bimestral).</summary>
    public string? Periodicidad { get; init; }

    /// <summary>Clave de c_Meses: 01 a 12 y, con periodicidad bimestral, 13 a 18.</summary>
    public string? Meses { get; init; }

    /// <summary>Año al que corresponde la información: el de la fecha del comprobante o el anterior.</summary>
    public int Anio { get; init; }
}

public sealed record SolicitudCfdiRelacionados
{
    /// <summary>Clave de c_TipoRelacion (01 Nota de crédito, 04 Sustitución...).</summary>
    public string? TipoRelacion { get; init; }

    /// <summary>Folios fiscales (UUID) de los comprobantes relacionados.</summary>
    public IReadOnlyList<string>? Uuids { get; init; }
}

public sealed record SolicitudConcepto
{
    /// <summary>Clave de c_ClaveProdServ.</summary>
    public string? ClaveProdServ { get; init; }

    public string? NoIdentificacion { get; init; }

    /// <summary>Hasta seis decimales.</summary>
    public decimal Cantidad { get; init; }

    /// <summary>Clave de c_ClaveUnidad (H87 Pieza, E48 Unidad de servicio...).</summary>
    public string? ClaveUnidad { get; init; }

    public string? Unidad { get; init; }

    public string? Descripcion { get; init; }

    /// <summary>Hasta seis decimales.</summary>
    public decimal ValorUnitario { get; init; }

    /// <summary>Descuento del concepto, hasta seis decimales y sin pasar del importe.</summary>
    public decimal? Descuento { get; init; }

    /// <summary>Clave de c_ObjetoImp. Si se omite: 02 cuando el concepto lleva impuestos y 01 cuando no.</summary>
    public string? ObjetoImp { get; init; }

    public IReadOnlyList<SolicitudImpuesto>? Traslados { get; init; }

    public IReadOnlyList<SolicitudImpuesto>? Retenciones { get; init; }

    internal SolicitudConcepto Normalizado()
    {
        var traslados = Traslados ?? [];
        var retenciones = Retenciones ?? [];
        return this with
        {
            ClaveProdServ = SolicitudCfdi.Limpio(ClaveProdServ),
            NoIdentificacion = SolicitudCfdi.Limpio(NoIdentificacion),
            ClaveUnidad = SolicitudCfdi.Limpio(ClaveUnidad)?.ToUpperInvariant(),
            Unidad = SolicitudCfdi.Limpio(Unidad),
            Descripcion = SolicitudCfdi.Limpio(Descripcion),
            Descuento = Descuento is 0m ? null : Descuento,
            ObjetoImp = SolicitudCfdi.Limpio(ObjetoImp) ?? (traslados.Count + retenciones.Count > 0 ? "02" : "01"),
            Traslados = traslados.Select(t => t.Normalizado()).ToList(),
            Retenciones = retenciones.Select(r => r.Normalizado()).ToList(),
        };
    }
}

/// <summary>Un traslado o una retención de un concepto; el importe lo calcula la app y la base también, salvo que se indique.</summary>
public sealed record SolicitudImpuesto
{
    /// <summary>Clave de c_Impuesto: 001 ISR, 002 IVA, 003 IEPS.</summary>
    public string? Impuesto { get; init; }

    /// <summary>Tasa, Cuota o Exento (las retenciones no pueden ser exentas).</summary>
    public string? TipoFactor { get; init; }

    /// <summary>La tasa (0.16 es el 16 %) o la cuota; hasta seis decimales. No se registra con Exento.</summary>
    public decimal? TasaOCuota { get; init; }

    /// <summary>
    /// Base del impuesto, solo si no es la habitual: con Tasa y Exento es el importe del concepto menos su descuento y con
    /// Cuota es la cantidad de unidades del concepto. Hasta seis decimales.
    /// </summary>
    public decimal? Base { get; init; }

    internal SolicitudImpuesto Normalizado() => this with
    {
        Impuesto = SolicitudCfdi.Limpio(Impuesto),
        TipoFactor = NombreDeFactor(SolicitudCfdi.Limpio(TipoFactor)),
        Base = Base is 0m ? null : Base,
    };

    /// <summary>"tasa", "TASA" y "Tasa" son lo mismo; cualquier otro texto se deja para que el validador lo señale.</summary>
    private static string? NombreDeFactor(string? factor) =>
        factor?.ToUpperInvariant() switch
        {
            "TASA" => "Tasa",
            "CUOTA" => "Cuota",
            "EXENTO" => "Exento",
            _ => factor,
        };
}
