namespace Comprobante.Api.Seguridad;

/// <summary>
/// Los límites de la API: la sección <c>Limites</c> de la configuración. Se leen con <c>IOptions</c> al atender cada
/// solicitud y no al armar la aplicación, para que una prueba (o una variable de entorno) los cambie sin tocar el código.
/// </summary>
internal sealed class LimitesOptions
{
    public const string Seccion = "Limites";

    /// <summary>Lo más que pesa el JSON de cálculo y de generación. El XML a validar tiene su propio límite, de 2 MB.</summary>
    public int JsonMaximoBytes { get; set; } = 1_048_576;

    public SolicitudesPorMinuto Solicitudes { get; set; } = new();
}

/// <summary>Cuántas solicitudes por minuto acepta la API de cada IP, con una cubeta para cada tipo de operación.</summary>
internal sealed class SolicitudesPorMinuto
{
    /// <summary><c>POST /api/comprobantes</c>: genera y sella.</summary>
    public int Generar { get; set; } = 20;

    /// <summary><c>POST /api/validaciones</c>: revisa un XML.</summary>
    public int Validar { get; set; } = 30;

    /// <summary><c>POST /api/comprobantes/calculo</c>: los totales en vivo mandan hasta 2,5 solicitudes por segundo.</summary>
    public int Calcular { get; set; } = 240;

    /// <summary>Catálogos, emisores de prueba y la lista de reglas.</summary>
    public int Consultar { get; set; } = 300;

    /// <summary>Todo lo demás, junto con lo anterior: el total de una IP. <c>/api/health</c> no cuenta.</summary>
    public int Global { get; set; } = 600;
}
