using System.Globalization;

namespace Comprobante.Core.Generacion;

/// <summary>
/// La hora local del lugar de expedición. El catálogo c_CodigoPostal dice el huso de cada código postal con uno de cinco
/// nombres; aquí cada uno se traduce a la zona de la base de datos tz que tiene las mismas reglas de horario.
/// </summary>
public static class ZonaHoraria
{
    public static string IdDe(string husoHorario) => husoHorario switch
    {
        "Tiempo del Pacífico" => "America/Mazatlan",
        "Tiempo del Noroeste" => "America/Tijuana",
        "Tiempo del Sureste" => "America/Cancun",
        "Tiempo del Centro en Frontera" => "America/Matamoros",
        _ => "America/Mexico_City",
    };

    /// <summary>La hora de pared en ese huso en el instante dado, sin zona (es lo que lleva el atributo Fecha).</summary>
    public static DateTime Local(DateTimeOffset instante, string husoHorario) =>
        TimeZoneInfo.ConvertTime(instante, TimeZoneInfo.FindSystemTimeZoneById(IdDe(husoHorario))).DateTime;

    /// <summary>La fecha como la escribe el CFDI: <c>2026-11-21T09:30:00</c>.</summary>
    public static string ComoFecha(DateTime local) => local.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
}
