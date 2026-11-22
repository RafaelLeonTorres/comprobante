using Comprobante.Api.Seguridad;
using Comprobante.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Comprobante.Api.Catalogos;

internal static class EndpointsCatalogos
{
    private const int TamanoMaximo = 100;

    public static void MapCatalogos(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/catalogos").WithTags("Catálogos del SAT");

        grupo.MapGet("/", ListarAsync)
            .WithSummary("Lista los catálogos del SAT con su número de filas y la versión cargada.")
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(LimiteDeSolicitudes.Consultar);

        grupo.MapGet("/{nombre}", ConsultarAsync)
            .WithSummary("Consulta un catálogo con búsqueda por texto y paginación.")
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(LimiteDeSolicitudes.Consultar);
    }

    private static async Task<IResult> ListarAsync(IDbContextFactory<CatalogosDbContext> fabrica, CancellationToken cancelacion)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);
        var catalogos = new List<object>();
        foreach (var descriptor in RegistroCatalogos.Todos)
        {
            catalogos.Add(new
            {
                nombre = descriptor.Nombre,
                titulo = descriptor.Titulo,
                filas = await descriptor.ContarAsync(db, cancelacion),
            });
        }

        var version = await db.VersionesCatalogos.AsNoTracking()
            .OrderByDescending(v => v.Id)
            .Select(v => new { v.Archivo, v.Publicacion, v.CargadoEn })
            .FirstOrDefaultAsync(cancelacion);
        return Results.Ok(new { version, catalogos });
    }

    private static async Task<IResult> ConsultarAsync(
        string nombre,
        IDbContextFactory<CatalogosDbContext> fabrica,
        CancellationToken cancelacion,
        string? q = null,
        int pagina = 1,
        int tamano = 25)
    {
        var descriptor = RegistroCatalogos.Buscar(nombre);
        if (descriptor is null)
        {
            return Results.Problem(
                title: "Catálogo desconocido",
                detail: $"No existe el catálogo \"{nombre}\". Los disponibles están en GET /api/catalogos.",
                statusCode: StatusCodes.Status404NotFound);
        }

        pagina = Math.Max(pagina, 1);
        tamano = Math.Clamp(tamano, 1, TamanoMaximo);
        await using var db = await fabrica.CreateDbContextAsync(cancelacion);
        return Results.Ok(await descriptor.ConsultarAsync(db, q, pagina, tamano, cancelacion));
    }
}
