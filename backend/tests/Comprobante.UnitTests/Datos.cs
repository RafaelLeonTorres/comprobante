using System.Globalization;
using Comprobante.Core.Solicitud;

namespace Comprobante.UnitTests;

/// <summary>Armar solicitudes y números de prueba sin repetir el mismo código en cada caso.</summary>
internal static class Datos
{
    public static decimal D(string valor) => decimal.Parse(valor, NumberStyles.Number, CultureInfo.InvariantCulture);

    public static SolicitudImpuesto Iva(decimal tasa = 0.16m) =>
        new() { Impuesto = "002", TipoFactor = "Tasa", TasaOCuota = tasa };

    public static SolicitudImpuesto IvaExento() => new() { Impuesto = "002", TipoFactor = "Exento" };

    public static SolicitudImpuesto Ieps(decimal tasa) =>
        new() { Impuesto = "003", TipoFactor = "Tasa", TasaOCuota = tasa };

    public static SolicitudImpuesto IepsCuota(decimal cuota) =>
        new() { Impuesto = "003", TipoFactor = "Cuota", TasaOCuota = cuota };

    public static SolicitudImpuesto RetencionIsr(decimal tasa) =>
        new() { Impuesto = "001", TipoFactor = "Tasa", TasaOCuota = tasa };

    public static SolicitudImpuesto RetencionIva(decimal tasa) =>
        new() { Impuesto = "002", TipoFactor = "Tasa", TasaOCuota = tasa };

    public static SolicitudConcepto Concepto(
        decimal cantidad,
        decimal valorUnitario,
        decimal? descuento = null,
        SolicitudImpuesto[]? traslados = null,
        SolicitudImpuesto[]? retenciones = null) =>
        new()
        {
            ClaveProdServ = "84111506",
            ClaveUnidad = "E48",
            Descripcion = "Servicio de prueba",
            Cantidad = cantidad,
            ValorUnitario = valorUnitario,
            Descuento = descuento,
            Traslados = traslados ?? [],
            Retenciones = retenciones ?? [],
        };

    public static SolicitudCfdi Solicitud(params SolicitudConcepto[] conceptos) => new() { Conceptos = conceptos };

    public static SolicitudCfdi EnMoneda(string moneda, params SolicitudConcepto[] conceptos) =>
        new() { Moneda = moneda, TipoCambio = moneda is "MXN" or "XXX" ? null : 17.5m, Conceptos = conceptos };
}
