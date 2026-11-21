namespace Comprobante.Core.Catalogos;

/// <summary>
/// Los catálogos chicos (ninguno pasa de 300 filas), completos y en memoria. Con ellos las reglas del validador
/// consultan sin ir a la base de datos. Los grandes (productos y servicios, unidades, códigos postales) se
/// consultan por clave con <see cref="ICatalogos"/>.
/// </summary>
public sealed class CatalogosPequenos
{
    public CatalogosPequenos(
        IEnumerable<FormaPago> formasPago,
        IEnumerable<Moneda> monedas,
        IEnumerable<TipoDeComprobante> tiposDeComprobante,
        IEnumerable<Exportacion> exportaciones,
        IEnumerable<MetodoPago> metodosPago,
        IEnumerable<Periodicidad> periodicidades,
        IEnumerable<Mes> meses,
        IEnumerable<TipoRelacion> tiposRelacion,
        IEnumerable<RegimenFiscal> regimenesFiscales,
        IEnumerable<Pais> paises,
        IEnumerable<UsoCfdi> usosCfdi,
        IEnumerable<ObjetoImp> objetosImp,
        IEnumerable<Impuesto> impuestos,
        IEnumerable<TipoFactor> tiposFactor,
        IEnumerable<TasaOCuota> tasasOCuotas,
        IEnumerable<Estado> estados)
    {
        FormasPago = formasPago.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        Monedas = monedas.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        TiposDeComprobante = tiposDeComprobante.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        Exportaciones = exportaciones.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        MetodosPago = metodosPago.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        Periodicidades = periodicidades.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        Meses = meses.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        TiposRelacion = tiposRelacion.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        RegimenesFiscales = regimenesFiscales.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        Paises = paises.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        UsosCfdi = usosCfdi.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        ObjetosImp = objetosImp.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        Impuestos = impuestos.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        TiposFactor = tiposFactor.ToDictionary(x => x.Clave, StringComparer.Ordinal);
        TasasOCuotas = tasasOCuotas.ToList();
        Estados = estados.ToList();
    }

    public IReadOnlyDictionary<string, FormaPago> FormasPago { get; }
    public IReadOnlyDictionary<string, Moneda> Monedas { get; }
    public IReadOnlyDictionary<string, TipoDeComprobante> TiposDeComprobante { get; }
    public IReadOnlyDictionary<string, Exportacion> Exportaciones { get; }
    public IReadOnlyDictionary<string, MetodoPago> MetodosPago { get; }
    public IReadOnlyDictionary<string, Periodicidad> Periodicidades { get; }
    public IReadOnlyDictionary<string, Mes> Meses { get; }
    public IReadOnlyDictionary<string, TipoRelacion> TiposRelacion { get; }
    public IReadOnlyDictionary<string, RegimenFiscal> RegimenesFiscales { get; }
    public IReadOnlyDictionary<string, Pais> Paises { get; }
    public IReadOnlyDictionary<string, UsoCfdi> UsosCfdi { get; }
    public IReadOnlyDictionary<string, ObjetoImp> ObjetosImp { get; }
    public IReadOnlyDictionary<string, Impuesto> Impuestos { get; }
    public IReadOnlyDictionary<string, TipoFactor> TiposFactor { get; }

    /// <summary>Sin clave natural: se busca por impuesto, factor y valor.</summary>
    public IReadOnlyList<TasaOCuota> TasasOCuotas { get; }

    public IReadOnlyList<Estado> Estados { get; }

    /// <summary>
    /// Si <paramref name="valor"/> es una tasa o cuota permitida para ese impuesto y factor: igual a un valor "Fijo" o
    /// dentro de un "Rango" del catálogo.
    /// </summary>
    public bool TasaOCuotaValida(string impuesto, string factor, decimal valor, bool traslado, DateOnly fecha) =>
        TasasOCuotas.Any(t =>
            t.Impuesto == impuesto
            && t.Factor == factor
            && (traslado ? t.Traslado : t.Retencion)
            && t.VigenteEn(fecha)
            && (t.RangoOFijo == "Fijo"
                ? valor == t.ValorMaximo
                : valor >= (t.ValorMinimo ?? 0m) && valor <= t.ValorMaximo));
}
