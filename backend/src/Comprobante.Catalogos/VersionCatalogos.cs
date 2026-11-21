namespace Comprobante.Catalogos;

/// <summary>Una carga de los catálogos del SAT: de qué Excel salieron y cuándo se metieron a la base de datos.</summary>
public sealed class VersionCatalogos
{
    public int Id { get; init; }

    /// <summary>Nombre del Excel del SAT del que se generaron los CSV (catCFDI_V_4_20261120.xls).</summary>
    public required string Archivo { get; init; }

    /// <summary>Fecha de publicación del Excel, si se pudo leer de su nombre.</summary>
    public DateOnly? Publicacion { get; init; }

    /// <summary>Huella SHA-256 de todos los CSV: si no cambia, no se vuelve a cargar nada.</summary>
    public required string Sha256 { get; init; }

    public DateTimeOffset CargadoEn { get; init; }
}
