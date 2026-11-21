namespace Comprobante.Core.Catalogos;

// Un registro por fila de cada catálogo del SAT que usan el generador y el validador. Son clases (no records)
// porque EF Core las carga y las guarda tal cual: sat/catalogos/*.csv -> PostgreSQL -> estos objetos.
// Los nombres de las propiedades siguen las columnas del CSV (clave, descripcion, inicio_vigencia...).

/// <summary>Un registro con fechas de vigencia: se puede usar desde <see cref="InicioVigencia"/> hasta <see cref="FinVigencia"/>.</summary>
public interface IVigente
{
    DateOnly InicioVigencia { get; }

    DateOnly? FinVigencia { get; }
}

public static class VigenciaExtensions
{
    /// <summary>Si el registro vale en esa fecha (sin fecha de fin, vale para siempre).</summary>
    public static bool VigenteEn(this IVigente registro, DateOnly fecha) =>
        fecha >= registro.InicioVigencia && (registro.FinVigencia is null || fecha <= registro.FinVigencia);
}

/// <summary>c_FormaPago: 01 Efectivo, 03 Transferencia, 99 Por definir...</summary>
public sealed class FormaPago : IVigente
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }
    public bool? Bancarizado { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}

/// <summary>c_Moneda: MXN, USD, XXX... con los decimales que admite y el margen del tipo de cambio.</summary>
public sealed class Moneda : IVigente
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }
    public required int Decimales { get; init; }
    public decimal? PorcentajeVariacion { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}

/// <summary>c_TipoDeComprobante: I Ingreso, E Egreso, T Traslado, N Nómina, P Pago.</summary>
public sealed class TipoDeComprobante : IVigente
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }

    /// <summary>Valor máximo del subtotal y del total; nulo en Nómina, donde el catálogo dice "NS".</summary>
    public decimal? ValorMaximo { get; init; }

    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}

/// <summary>c_Exportacion: 01 No aplica, 02 Definitiva con clave A1, 03 Temporal, 04 Definitiva con otra clave.</summary>
public sealed class Exportacion : IVigente
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}

/// <summary>c_MetodoPago: PUE Pago en una sola exhibición, PPD Pago en parcialidades o diferido.</summary>
public sealed class MetodoPago : IVigente
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}

/// <summary>c_CodigoPostal: cada código postal con su estado, municipio y el huso horario donde se expide el comprobante.</summary>
public sealed class CodigoPostal : IVigente
{
    public required string Clave { get; init; }
    public required string Estado { get; init; }
    public string? Municipio { get; init; }
    public string? Localidad { get; init; }
    public bool? EstimuloFranjaFronteriza { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }

    /// <summary>"Tiempo del Centro", "Tiempo del Pacífico", "Tiempo del Noroeste", "Tiempo del Sureste" o "Tiempo del Centro en Frontera".</summary>
    public required string HusoHorario { get; init; }

    public string? MesInicioVerano { get; init; }
    public string? DiaInicioVerano { get; init; }
    public string? HoraInicioVerano { get; init; }

    /// <summary>Horas respecto a UTC durante el horario de verano (por ejemplo -6).</summary>
    public int? DiferenciaVerano { get; init; }

    public string? MesInicioInvierno { get; init; }
    public string? DiaInicioInvierno { get; init; }
    public string? HoraInicioInvierno { get; init; }

    /// <summary>Horas respecto a UTC durante el horario de invierno.</summary>
    public int? DiferenciaInvierno { get; init; }
}

/// <summary>c_Periodicidad: 01 Diario ... 05 Bimestral (solo para la información global).</summary>
public sealed class Periodicidad : IVigente
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}

/// <summary>c_Meses: 01 Enero ... 12 Diciembre, y 13 a 18 para los bimestres.</summary>
public sealed class Mes : IVigente
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}

/// <summary>c_TipoRelacion: 01 Nota de crédito, 04 Sustitución de los CFDI previos...</summary>
public sealed class TipoRelacion : IVigente
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}

/// <summary>c_RegimenFiscal: 601, 612, 626... y si aplica a personas físicas, morales o ambas.</summary>
public sealed class RegimenFiscal : IVigente
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }
    public required bool Fisica { get; init; }
    public required bool Moral { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}

/// <summary>c_Pais: MEX, USA, ZZZ... con el formato del código postal y del registro de identidad tributaria.</summary>
public sealed class Pais
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }
    public string? FormatoCodigoPostal { get; init; }
    public string? FormatoRegistroIdentidadTributaria { get; init; }
    public string? ValidacionRegistroIdentidadTributaria { get; init; }
    public string? Agrupaciones { get; init; }
}

/// <summary>c_UsoCFDI: G01, G03, S01... con los tipos de persona y los regímenes del receptor a los que aplica.</summary>
public sealed class UsoCfdi : IVigente
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }
    public required bool Fisica { get; init; }
    public required bool Moral { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }

    /// <summary>Claves de c_RegimenFiscal que pueden usar esta clave de uso.</summary>
    public required string[] RegimenesReceptor { get; init; }
}

/// <summary>c_ClaveProdServ: las más de 52 mil claves de productos y servicios.</summary>
public sealed class ClaveProdServ : IVigente
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }

    /// <summary>"Sí", "No" u "Opcional": si el concepto debe llevar IVA trasladado.</summary>
    public string? IncluirIva { get; init; }

    /// <summary>"Sí", "No" u "Opcional": si el concepto debe llevar IEPS trasladado.</summary>
    public string? IncluirIeps { get; init; }

    /// <summary>Complemento que el comprobante debe incluir con esta clave (por ejemplo "Carta Porte").</summary>
    public string? Complemento { get; init; }

    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
    public bool? EstimuloFranjaFronteriza { get; init; }
    public string? PalabrasSimilares { get; init; }
}

/// <summary>c_ClaveUnidad: H87 Pieza, E48 Unidad de servicio, KGM Kilogramo...</summary>
public sealed class ClaveUnidad : IVigente
{
    public required string Clave { get; init; }
    public required string Nombre { get; init; }
    public string? Descripcion { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
    public string? Simbolo { get; init; }
}

/// <summary>c_ObjetoImp: 01 No objeto de impuesto, 02 Sí objeto de impuesto...</summary>
public sealed class ObjetoImp : IVigente
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}

/// <summary>c_Impuesto: 001 ISR, 002 IVA, 003 IEPS.</summary>
public sealed class Impuesto : IVigente
{
    public required string Clave { get; init; }
    public required string Descripcion { get; init; }
    public required bool Retencion { get; init; }
    public required bool Traslado { get; init; }
    public required string LocalOFederal { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}

/// <summary>c_TipoFactor: Tasa, Cuota o Exento.</summary>
public sealed class TipoFactor : IVigente
{
    public required string Clave { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}

/// <summary>
/// c_TasaOCuota: los valores que puede llevar TasaOCuota. Un valor "Fijo" es exactamente <see cref="ValorMaximo"/>;
/// uno de "Rango" es cualquier valor entre <see cref="ValorMinimo"/> y <see cref="ValorMaximo"/>.
/// </summary>
public sealed class TasaOCuota : IVigente
{
    /// <summary>Número de fila, para identificar el registro (el catálogo no tiene clave).</summary>
    public int Id { get; init; }

    /// <summary>"Fijo" o "Rango".</summary>
    public required string RangoOFijo { get; init; }

    public decimal? ValorMinimo { get; init; }
    public required decimal ValorMaximo { get; init; }

    /// <summary>Clave de c_Impuesto (001, 002, 003).</summary>
    public required string Impuesto { get; init; }

    /// <summary>"Tasa" o "Cuota".</summary>
    public required string Factor { get; init; }

    public required bool Traslado { get; init; }
    public required bool Retencion { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}

/// <summary>c_Estado: estados de México, Estados Unidos y Canadá (la clave se repite entre países).</summary>
public sealed class Estado : IVigente
{
    public required string Pais { get; init; }
    public required string Clave { get; init; }
    public required string Nombre { get; init; }
    public required DateOnly InicioVigencia { get; init; }
    public DateOnly? FinVigencia { get; init; }
}
