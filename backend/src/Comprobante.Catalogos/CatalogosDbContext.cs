using Comprobante.Core.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Comprobante.Catalogos;

/// <summary>Los catálogos del SAT en PostgreSQL. Cada tabla tiene el nombre de su CSV en sat/catalogos.</summary>
public sealed class CatalogosDbContext(DbContextOptions<CatalogosDbContext> opciones) : DbContext(opciones)
{
    public DbSet<FormaPago> FormasPago => Set<FormaPago>();
    public DbSet<Moneda> Monedas => Set<Moneda>();
    public DbSet<TipoDeComprobante> TiposDeComprobante => Set<TipoDeComprobante>();
    public DbSet<Exportacion> Exportaciones => Set<Exportacion>();
    public DbSet<MetodoPago> MetodosPago => Set<MetodoPago>();
    public DbSet<CodigoPostal> CodigosPostales => Set<CodigoPostal>();
    public DbSet<Periodicidad> Periodicidades => Set<Periodicidad>();
    public DbSet<Mes> Meses => Set<Mes>();
    public DbSet<TipoRelacion> TiposRelacion => Set<TipoRelacion>();
    public DbSet<RegimenFiscal> RegimenesFiscales => Set<RegimenFiscal>();
    public DbSet<Pais> Paises => Set<Pais>();
    public DbSet<UsoCfdi> UsosCfdi => Set<UsoCfdi>();
    public DbSet<ClaveProdServ> ClavesProdServ => Set<ClaveProdServ>();
    public DbSet<ClaveUnidad> ClavesUnidad => Set<ClaveUnidad>();
    public DbSet<ObjetoImp> ObjetosImp => Set<ObjetoImp>();
    public DbSet<Impuesto> Impuestos => Set<Impuesto>();
    public DbSet<TipoFactor> TiposFactor => Set<TipoFactor>();
    public DbSet<TasaOCuota> TasasOCuotas => Set<TasaOCuota>();
    public DbSet<Estado> Estados => Set<Estado>();
    public DbSet<VersionCatalogos> VersionesCatalogos => Set<VersionCatalogos>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        // pg_trgm permite buscar "%texto%" con índice, que es lo que hace la página de catálogos con las 52 mil claves.
        modelo.HasPostgresExtension("pg_trgm");

        modelo.Entity<FormaPago>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(2);
            e.Property(x => x.Descripcion).HasMaxLength(200);
        });

        modelo.Entity<Moneda>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(3);
            e.Property(x => x.Descripcion).HasMaxLength(200);
            e.Property(x => x.PorcentajeVariacion).HasPrecision(8, 4);
        });

        modelo.Entity<TipoDeComprobante>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(1);
            e.Property(x => x.Descripcion).HasMaxLength(200);
            e.Property(x => x.ValorMaximo).HasPrecision(24, 6);
        });

        modelo.Entity<Exportacion>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(2);
            e.Property(x => x.Descripcion).HasMaxLength(200);
        });

        modelo.Entity<MetodoPago>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(3);
            e.Property(x => x.Descripcion).HasMaxLength(200);
        });

        modelo.Entity<CodigoPostal>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(5);
            e.Property(x => x.Estado).HasMaxLength(3);
            e.Property(x => x.Municipio).HasMaxLength(3);
            e.Property(x => x.Localidad).HasMaxLength(2);
            e.Property(x => x.HusoHorario).HasMaxLength(50);
            e.Property(x => x.MesInicioVerano).HasMaxLength(20);
            e.Property(x => x.DiaInicioVerano).HasMaxLength(30);
            e.Property(x => x.HoraInicioVerano).HasMaxLength(10);
            e.Property(x => x.MesInicioInvierno).HasMaxLength(20);
            e.Property(x => x.DiaInicioInvierno).HasMaxLength(30);
            e.Property(x => x.HoraInicioInvierno).HasMaxLength(10);
        });

        modelo.Entity<Periodicidad>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(2);
            e.Property(x => x.Descripcion).HasMaxLength(200);
        });

        modelo.Entity<Mes>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(2);
            e.Property(x => x.Descripcion).HasMaxLength(200);
        });

        modelo.Entity<TipoRelacion>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(2);
            e.Property(x => x.Descripcion).HasMaxLength(200);
        });

        modelo.Entity<RegimenFiscal>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(3);
            e.Property(x => x.Descripcion).HasMaxLength(200);
        });

        modelo.Entity<Pais>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(3);
            e.Property(x => x.Descripcion).HasMaxLength(200);
        });

        modelo.Entity<UsoCfdi>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(4);
            e.Property(x => x.Descripcion).HasMaxLength(200);
        });

        modelo.Entity<ClaveProdServ>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(8);
            e.Property(x => x.Descripcion).HasMaxLength(500);
            e.Property(x => x.IncluirIva).HasMaxLength(20);
            e.Property(x => x.IncluirIeps).HasMaxLength(20);
            e.Property(x => x.Complemento).HasMaxLength(500);
            e.HasIndex(x => x.Descripcion).HasMethod("gin").HasOperators("gin_trgm_ops");
        });

        modelo.Entity<ClaveUnidad>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(3);
            e.Property(x => x.Nombre).HasMaxLength(300);
            e.Property(x => x.Descripcion).HasMaxLength(2000);
            e.Property(x => x.Simbolo).HasMaxLength(100);
            e.HasIndex(x => x.Nombre).HasMethod("gin").HasOperators("gin_trgm_ops");
        });

        modelo.Entity<ObjetoImp>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(2);
            e.Property(x => x.Descripcion).HasMaxLength(200);
        });

        modelo.Entity<Impuesto>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(3);
            e.Property(x => x.Descripcion).HasMaxLength(100);
            e.Property(x => x.LocalOFederal).HasMaxLength(20);
        });

        modelo.Entity<TipoFactor>(e =>
        {
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasMaxLength(10);
        });

        modelo.Entity<TasaOCuota>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.RangoOFijo).HasMaxLength(10);
            e.Property(x => x.ValorMinimo).HasPrecision(18, 6);
            e.Property(x => x.ValorMaximo).HasPrecision(18, 6);
            e.Property(x => x.Impuesto).HasMaxLength(3);
            e.Property(x => x.Factor).HasMaxLength(10);
            e.HasIndex(x => new { x.Impuesto, x.Factor });
        });

        modelo.Entity<Estado>(e =>
        {
            e.HasKey(x => new { x.Pais, x.Clave });
            e.Property(x => x.Pais).HasMaxLength(3);
            e.Property(x => x.Clave).HasMaxLength(3);
            e.Property(x => x.Nombre).HasMaxLength(200);
        });

        modelo.Entity<VersionCatalogos>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Archivo).HasMaxLength(200);
            e.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
        });

        NombresSnakeCase.Aplicar(modelo.Model);
    }
}
