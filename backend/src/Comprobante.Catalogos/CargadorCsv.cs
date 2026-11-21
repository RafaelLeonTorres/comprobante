using Comprobante.Core.Catalogos;

namespace Comprobante.Catalogos;

/// <summary>
/// Convierte los CSV de sat/catalogos (incrustados en el ensamblado) en los objetos de <c>Comprobante.Core.Catalogos</c>.
/// Lo usan el sembrador, que los guarda en PostgreSQL, y las pruebas, que los usan directo sin base de datos.
/// </summary>
public static class CargadorCsv
{
    private static List<T> Leer<T>(string archivo, Func<FilaCsv, T> convertir)
    {
        using var flujo = RecursosCatalogos.Abrir(archivo);
        return LectorCsv.Leer(flujo, archivo).Select(convertir).ToList();
    }

    public static List<FormaPago> FormasPago() => Leer("forma_pago.csv", f => new FormaPago
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        Bancarizado = f.BooleanoOpcional("bancarizado"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    public static List<Moneda> Monedas() => Leer("moneda.csv", f => new Moneda
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        Decimales = f.Entero("decimales"),
        PorcentajeVariacion = f.DecimalOpcional("porcentaje_variacion"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    public static List<TipoDeComprobante> TiposDeComprobante() => Leer("tipo_de_comprobante.csv", f => new TipoDeComprobante
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        ValorMaximo = f.DecimalOpcional("valor_maximo"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    public static List<Exportacion> Exportaciones() => Leer("exportacion.csv", f => new Exportacion
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    public static List<MetodoPago> MetodosPago() => Leer("metodo_pago.csv", f => new MetodoPago
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    public static List<CodigoPostal> CodigosPostales() => Leer("codigo_postal.csv", f => new CodigoPostal
    {
        Clave = f.Texto("clave"),
        Estado = f.Texto("estado"),
        Municipio = f.TextoOpcional("municipio"),
        Localidad = f.TextoOpcional("localidad"),
        EstimuloFranjaFronteriza = f.BooleanoOpcional("estimulo_franja_fronteriza"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
        HusoHorario = f.Texto("huso_horario"),
        MesInicioVerano = f.TextoOpcional("mes_inicio_verano"),
        DiaInicioVerano = f.TextoOpcional("dia_inicio_verano"),
        HoraInicioVerano = f.TextoOpcional("hora_inicio_verano"),
        DiferenciaVerano = f.EnteroOpcional("diferencia_verano"),
        MesInicioInvierno = f.TextoOpcional("mes_inicio_invierno"),
        DiaInicioInvierno = f.TextoOpcional("dia_inicio_invierno"),
        HoraInicioInvierno = f.TextoOpcional("hora_inicio_invierno"),
        DiferenciaInvierno = f.EnteroOpcional("diferencia_invierno"),
    });

    public static List<Periodicidad> Periodicidades() => Leer("periodicidad.csv", f => new Periodicidad
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    public static List<Mes> Meses() => Leer("meses.csv", f => new Mes
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    public static List<TipoRelacion> TiposRelacion() => Leer("tipo_relacion.csv", f => new TipoRelacion
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    public static List<RegimenFiscal> RegimenesFiscales() => Leer("regimen_fiscal.csv", f => new RegimenFiscal
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        Fisica = f.Booleano("fisica"),
        Moral = f.Booleano("moral"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    public static List<Pais> Paises() => Leer("pais.csv", f => new Pais
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        FormatoCodigoPostal = f.TextoOpcional("formato_codigo_postal"),
        FormatoRegistroIdentidadTributaria = f.TextoOpcional("formato_registro_identidad_tributaria"),
        ValidacionRegistroIdentidadTributaria = f.TextoOpcional("validacion_registro_identidad_tributaria"),
        Agrupaciones = f.TextoOpcional("agrupaciones"),
    });

    public static List<UsoCfdi> UsosCfdi() => Leer("uso_cfdi.csv", f => new UsoCfdi
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        Fisica = f.Booleano("fisica"),
        Moral = f.Booleano("moral"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
        RegimenesReceptor = f.Lista("regimenes_receptor"),
    });

    public static List<ClaveProdServ> ClavesProdServ() => Leer("clave_prod_serv.csv", f => new ClaveProdServ
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        IncluirIva = f.TextoOpcional("incluir_iva"),
        IncluirIeps = f.TextoOpcional("incluir_ieps"),
        Complemento = f.TextoOpcional("complemento"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
        EstimuloFranjaFronteriza = f.BooleanoOpcional("estimulo_franja_fronteriza"),
        PalabrasSimilares = f.TextoOpcional("palabras_similares"),
    });

    public static List<ClaveUnidad> ClavesUnidad() => Leer("clave_unidad.csv", f => new ClaveUnidad
    {
        Clave = f.Texto("clave"),
        Nombre = f.Texto("nombre"),
        Descripcion = f.TextoOpcional("descripcion"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
        Simbolo = f.TextoOpcional("simbolo"),
    });

    public static List<ObjetoImp> ObjetosImp() => Leer("objeto_imp.csv", f => new ObjetoImp
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    public static List<Impuesto> Impuestos() => Leer("impuesto.csv", f => new Impuesto
    {
        Clave = f.Texto("clave"),
        Descripcion = f.Texto("descripcion"),
        Retencion = f.Booleano("retencion"),
        Traslado = f.Booleano("traslado"),
        LocalOFederal = f.Texto("local_o_federal"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    public static List<TipoFactor> TiposFactor() => Leer("tipo_factor.csv", f => new TipoFactor
    {
        Clave = f.Texto("clave"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    public static List<TasaOCuota> TasasOCuotas() => Leer("tasa_o_cuota.csv", f => new TasaOCuota
    {
        RangoOFijo = f.Texto("rango_o_fijo"),
        ValorMinimo = f.DecimalOpcional("valor_minimo"),
        ValorMaximo = f.Decimal("valor_maximo"),
        Impuesto = f.Texto("impuesto"),
        Factor = f.Texto("factor"),
        Traslado = f.Booleano("traslado"),
        Retencion = f.Booleano("retencion"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    public static List<Estado> Estados() => Leer("estado.csv", f => new Estado
    {
        Pais = f.Texto("pais"),
        Clave = f.Texto("clave"),
        Nombre = f.Texto("nombre"),
        InicioVigencia = f.Fecha("inicio_vigencia"),
        FinVigencia = f.FechaOpcional("fin_vigencia"),
    });

    /// <summary>Los 16 catálogos chicos, listos para usarse sin base de datos.</summary>
    public static CatalogosPequenos Pequenos() => new(
        FormasPago(), Monedas(), TiposDeComprobante(), Exportaciones(), MetodosPago(), Periodicidades(), Meses(),
        TiposRelacion(), RegimenesFiscales(), Paises(), UsosCfdi(), ObjetosImp(), Impuestos(), TiposFactor(),
        TasasOCuotas(), Estados());
}
