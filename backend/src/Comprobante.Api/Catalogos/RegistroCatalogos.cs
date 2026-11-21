using System.Linq.Expressions;
using Comprobante.Catalogos;
using Comprobante.Core.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Comprobante.Api.Catalogos;

internal sealed record ColumnaCatalogo(string Clave, string Titulo);

internal sealed record PaginaCatalogo(
    int Total,
    int Pagina,
    int Tamano,
    IReadOnlyList<ColumnaCatalogo> Columnas,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Filas);

/// <summary>Cómo se lista y se busca un catálogo del SAT en la API.</summary>
internal abstract class DescriptorCatalogo(string nombre, string titulo)
{
    /// <summary>Lo que va en la URL: /api/catalogos/{nombre}.</summary>
    public string Nombre { get; } = nombre;

    /// <summary>El nombre del SAT, como "Forma de pago (c_FormaPago)".</summary>
    public string Titulo { get; } = titulo;

    public abstract Task<int> ContarAsync(CatalogosDbContext db, CancellationToken cancelacion);

    public abstract Task<PaginaCatalogo> ConsultarAsync(
        CatalogosDbContext db, string? texto, int pagina, int tamano, CancellationToken cancelacion);
}

internal sealed class DescriptorCatalogo<T>(
    string nombre,
    string titulo,
    Func<CatalogosDbContext, DbSet<T>> tabla,
    Func<string, Expression<Func<T, bool>>> filtro,
    Func<IQueryable<T>, IOrderedQueryable<T>> orden,
    params (string Clave, string Titulo, Func<T, object?> Valor)[] columnas)
    : DescriptorCatalogo(nombre, titulo)
    where T : class
{
    public override Task<int> ContarAsync(CatalogosDbContext db, CancellationToken cancelacion) =>
        tabla(db).CountAsync(cancelacion);

    public override async Task<PaginaCatalogo> ConsultarAsync(
        CatalogosDbContext db, string? texto, int pagina, int tamano, CancellationToken cancelacion)
    {
        IQueryable<T> consulta = tabla(db).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            consulta = consulta.Where(filtro(Patron(texto)));
        }

        var total = await consulta.CountAsync(cancelacion);
        var registros = await orden(consulta).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(cancelacion);
        var filas = registros
            .Select(r => (IReadOnlyDictionary<string, object?>)columnas.ToDictionary(c => c.Clave, c => c.Valor(r)))
            .ToList();
        return new PaginaCatalogo(
            total, pagina, tamano, columnas.Select(c => new ColumnaCatalogo(c.Clave, c.Titulo)).ToList(), filas);
    }

    /// <summary>"%texto%" para ILIKE, con los comodines que el usuario haya escrito como texto literal.</summary>
    private static string Patron(string texto) =>
        "%" + texto.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}

/// <summary>Los 19 catálogos que sirve la API, en el orden en que los muestra la página de catálogos.</summary>
internal static class RegistroCatalogos
{
    private static string? Fecha(DateOnly? fecha) => fecha?.ToString("yyyy-MM-dd");

    public static readonly IReadOnlyList<DescriptorCatalogo> Todos =
    [
        new DescriptorCatalogo<TipoDeComprobante>("tipo-de-comprobante", "Tipo de comprobante (c_TipoDeComprobante)",
            db => db.TiposDeComprobante, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion),
            ("valorMaximo", "Valor máximo", x => x.ValorMaximo)),
        new DescriptorCatalogo<FormaPago>("forma-pago", "Forma de pago (c_FormaPago)",
            db => db.FormasPago, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion),
            ("bancarizado", "Bancarizado", x => x.Bancarizado)),
        new DescriptorCatalogo<MetodoPago>("metodo-pago", "Método de pago (c_MetodoPago)",
            db => db.MetodosPago, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion)),
        new DescriptorCatalogo<Moneda>("moneda", "Moneda (c_Moneda)",
            db => db.Monedas, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion),
            ("decimales", "Decimales", x => x.Decimales), ("variacion", "% de variación", x => x.PorcentajeVariacion)),
        new DescriptorCatalogo<Exportacion>("exportacion", "Exportación (c_Exportacion)",
            db => db.Exportaciones, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion)),
        new DescriptorCatalogo<RegimenFiscal>("regimen-fiscal", "Régimen fiscal (c_RegimenFiscal)",
            db => db.RegimenesFiscales, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion),
            ("fisica", "Persona física", x => x.Fisica), ("moral", "Persona moral", x => x.Moral)),
        new DescriptorCatalogo<UsoCfdi>("uso-cfdi", "Uso del CFDI (c_UsoCFDI)",
            db => db.UsosCfdi, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion),
            ("fisica", "Persona física", x => x.Fisica), ("moral", "Persona moral", x => x.Moral),
            ("regimenes", "Regímenes del receptor", x => string.Join(", ", x.RegimenesReceptor))),
        new DescriptorCatalogo<ObjetoImp>("objeto-imp", "Objeto de impuesto (c_ObjetoImp)",
            db => db.ObjetosImp, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion)),
        new DescriptorCatalogo<Impuesto>("impuesto", "Impuesto (c_Impuesto)",
            db => db.Impuestos, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion),
            ("retencion", "Retención", x => x.Retencion), ("traslado", "Traslado", x => x.Traslado),
            ("ambito", "Local o federal", x => x.LocalOFederal)),
        new DescriptorCatalogo<TipoFactor>("tipo-factor", "Tipo de factor (c_TipoFactor)",
            db => db.TiposFactor, p => x => EF.Functions.ILike(x.Clave, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave)),
        new DescriptorCatalogo<TasaOCuota>("tasa-o-cuota", "Tasa o cuota (c_TasaOCuota)",
            db => db.TasasOCuotas, p => x => EF.Functions.ILike(x.Impuesto, p) || EF.Functions.ILike(x.Factor, p),
            q => q.OrderBy(x => x.Id),
            ("rangoOFijo", "Rango o fijo", x => x.RangoOFijo), ("minimo", "Mínimo", x => x.ValorMinimo),
            ("maximo", "Máximo (o valor fijo)", x => x.ValorMaximo), ("impuesto", "Impuesto", x => x.Impuesto),
            ("factor", "Factor", x => x.Factor), ("traslado", "Traslado", x => x.Traslado),
            ("retencion", "Retención", x => x.Retencion)),
        new DescriptorCatalogo<Periodicidad>("periodicidad", "Periodicidad (c_Periodicidad)",
            db => db.Periodicidades, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion)),
        new DescriptorCatalogo<Mes>("meses", "Meses (c_Meses)",
            db => db.Meses, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion)),
        new DescriptorCatalogo<TipoRelacion>("tipo-relacion", "Tipo de relación (c_TipoRelacion)",
            db => db.TiposRelacion, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion)),
        new DescriptorCatalogo<Pais>("pais", "País (c_Pais)",
            db => db.Paises, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Descripcion),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion),
            ("agrupaciones", "Agrupaciones", x => x.Agrupaciones)),
        new DescriptorCatalogo<Estado>("estado", "Estado (c_Estado)",
            db => db.Estados, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Nombre, p) || EF.Functions.ILike(x.Pais, p),
            q => q.OrderBy(x => x.Pais).ThenBy(x => x.Clave),
            ("pais", "País", x => x.Pais), ("clave", "Clave", x => x.Clave), ("nombre", "Nombre", x => x.Nombre)),
        new DescriptorCatalogo<ClaveUnidad>("clave-unidad", "Clave de unidad (c_ClaveUnidad)",
            db => db.ClavesUnidad, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Nombre, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("nombre", "Nombre", x => x.Nombre), ("simbolo", "Símbolo", x => x.Simbolo)),
        new DescriptorCatalogo<ClaveProdServ>("clave-prod-serv", "Clave de producto o servicio (c_ClaveProdServ)",
            db => db.ClavesProdServ,
            p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Descripcion, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Clave", x => x.Clave), ("descripcion", "Descripción", x => x.Descripcion),
            ("iva", "IVA trasladado", x => x.IncluirIva), ("ieps", "IEPS trasladado", x => x.IncluirIeps),
            ("complemento", "Complemento", x => x.Complemento)),
        new DescriptorCatalogo<CodigoPostal>("codigo-postal", "Código postal (c_CodigoPostal)",
            db => db.CodigosPostales, p => x => EF.Functions.ILike(x.Clave, p) || EF.Functions.ILike(x.Estado, p),
            q => q.OrderBy(x => x.Clave),
            ("clave", "Código postal", x => x.Clave), ("estado", "Estado", x => x.Estado),
            ("municipio", "Municipio", x => x.Municipio), ("huso", "Huso horario", x => x.HusoHorario),
            ("frontera", "Estímulo frontera", x => x.EstimuloFranjaFronteriza)),
    ];

    public static DescriptorCatalogo? Buscar(string nombre) =>
        Todos.FirstOrDefault(d => string.Equals(d.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
}
