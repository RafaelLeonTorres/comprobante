namespace Comprobante.Core.Representacion;

/// <summary>
/// Todo lo que dice la representación impresa de un CFDI, ya escrito como se va a leer: las claves con la descripción de
/// su catálogo ("612 - Personas Físicas con Actividades Empresariales y Profesionales"), los importes con separador de
/// miles, los impuestos de cada concepto, el importe con letra y la dirección del código QR. Quien dibuja el PDF
/// (Comprobante.Pdf) solo acomoda estos textos en la página: no consulta catálogos ni hace cuentas.
/// </summary>
public sealed record RepresentacionImpresa
{
    /// <summary>Lo que dice el encabezado en grande: FACTURA, NOTA DE CRÉDITO, TRASLADO, NÓMINA o PAGO.</summary>
    public required string Titulo { get; init; }

    /// <summary>Si el XML trae el timbre del SAT. Sin timbre, el PDF dice que no tiene validez fiscal.</summary>
    public required bool EstaTimbrado { get; init; }

    /// <summary>El UUID del timbre; nulo si el comprobante no está timbrado.</summary>
    public string? FolioFiscal { get; init; }

    /// <summary>Serie y folio juntos ("H-0042"); nulo si el comprobante no trae ninguno.</summary>
    public string? SerieFolio { get; init; }

    public required string Version { get; init; }

    public required ParteImpresa Emisor { get; init; }

    public required ParteImpresa Receptor { get; init; }

    /// <summary>Los datos del comprobante que van en la cuadrícula debajo del emisor y el receptor, en el orden en que se leen.</summary>
    public required IReadOnlyList<DatoImpreso> Datos { get; init; }

    /// <summary>Los CFDI relacionados, uno por tipo de relación. Vacío si el comprobante no relaciona ninguno.</summary>
    public IReadOnlyList<RelacionImpresa> Relacionados { get; init; } = [];

    public required IReadOnlyList<ConceptoImpreso> Conceptos { get; init; }

    public required TotalesImpresos Totales { get; init; }

    /// <summary>El resumen de impuestos trasladados del comprobante.</summary>
    public IReadOnlyList<ImpuestoImpreso> Traslados { get; init; } = [];

    /// <summary>El resumen de impuestos retenidos del comprobante.</summary>
    public IReadOnlyList<ImpuestoImpreso> Retenciones { get; init; } = [];

    public required SellosImpresos Sellos { get; init; }

    /// <summary>El nombre con el que se descarga el PDF: <c>RFC-Serie-Folio-prueba.pdf</c>, sin "-prueba" si está timbrado.</summary>
    public required string NombreDeArchivo { get; init; }

    /// <summary>El título que lleva el PDF en sus propiedades ("Factura H-0042").</summary>
    public required string TituloDelDocumento { get; init; }

    /// <summary>El asunto que lleva el PDF en sus propiedades; dice que no tiene validez fiscal cuando no está timbrado.</summary>
    public required string AsuntoDelDocumento { get; init; }
}

/// <summary>Cómo se resalta un dato: el nombre en negritas y las claves (RFC, UUID, certificados) en letra monoespaciada.</summary>
public enum Enfasis
{
    Normal,
    Nombre,
    Codigo,
}

/// <summary>Un dato con su etiqueta, como "Régimen fiscal" y "612 - Personas Físicas con Actividades...".</summary>
public sealed record DatoImpreso(string Etiqueta, string Valor, Enfasis Enfasis = Enfasis.Normal);

/// <summary>El emisor o el receptor: su título ("Emisor") y sus datos.</summary>
public sealed record ParteImpresa(string Titulo, IReadOnlyList<DatoImpreso> Datos);

/// <summary>Los CFDI que este comprobante relaciona con un tipo de relación ("01 - Nota de crédito de los documentos relacionados").</summary>
public sealed record RelacionImpresa(string Tipo, IReadOnlyList<string> Uuids);

/// <summary>
/// Un renglón de la tabla de conceptos. Los importes van sin símbolo (la moneda está en los datos del comprobante);
/// <paramref name="Impuestos"/> trae una línea por impuesto del concepto, lista para ponerse en letra chica.
/// </summary>
public sealed record ConceptoImpreso(
    string ClaveProdServ,
    string Unidad,
    string Cantidad,
    string? NoIdentificacion,
    string Descripcion,
    string ValorUnitario,
    string? Descuento,
    string ObjetoImp,
    string Importe,
    IReadOnlyList<string> Impuestos);

/// <summary>Un renglón del resumen de impuestos. Lo que no aplica (el exento no tiene tasa ni importe) va nulo.</summary>
public sealed record ImpuestoImpreso(string? Base, string Impuesto, string? Factor, string? TasaOCuota, string? Importe);

/// <summary>Los totales con símbolo de moneda; el importe con letra es nulo cuando el comprobante no tiene moneda (XXX).</summary>
public sealed record TotalesImpresos(
    string Subtotal,
    string? Descuento,
    string? TotalTrasladados,
    string? TotalRetenidos,
    string Total,
    string? ImporteConLetra);

/// <summary>
/// Lo que va al pie: el código QR (solo con timbre), la cadena original que se firmó y los sellos. Con timbre, la cadena es
/// la del complemento de certificación del SAT; sin él, la del comprobante.
/// </summary>
public sealed record SellosImpresos(
    string? UrlDeVerificacion,
    string TituloDeLaCadena,
    string Cadena,
    string? SelloDelEmisor,
    string? SelloDelSat);
