namespace Comprobante.Core.Calculo;

/// <summary>
/// Los límites inferior y superior del Anexo 20 (validaciones adicionales a realizar por el proveedor): el valor que
/// registra el emisor tiene que caer entre los dos. Se calculan con los decimales con que viene escrito cada campo,
/// de modo que un sistema que redondea a seis decimales y otro que lo hace a dos pasan la misma regla.
/// </summary>
public static class Limites
{
    /// <summary>Un billonésimo, 10 elevado a la menos 12: la fórmula del SAT lo resta para que el límite superior no se pase.</summary>
    private const decimal Piso = 0.000000000001m;

    public readonly record struct Rango(decimal Inferior, decimal Superior)
    {
        public bool Contiene(decimal valor) => valor >= Inferior && valor <= Superior;
    }

    /// <summary>
    /// Límites del Importe de un concepto (y de una parte): el inferior es (Cantidad - 10^-NumDecimalesCantidad / 2) por
    /// (ValorUnitario - 10^-NumDecimalesValorUnitario / 2), truncado a los decimales del Importe; el superior es
    /// (Cantidad + 10^-NumDecimalesCantidad / 2 - 10^-12) por (ValorUnitario + 10^-NumDecimalesValorUnitario / 2 - 10^-12),
    /// redondeado hacia arriba a los decimales del Importe. Devuelve null si las cifras son tan grandes que no caben en un decimal.
    /// </summary>
    public static Rango? ImporteConcepto(
        decimal cantidad, int decimalesCantidad, decimal valorUnitario, int decimalesValorUnitario, int decimalesImporte)
    {
        try
        {
            var mediaCantidad = MitadDeLaUltimaCifra(decimalesCantidad);
            var mediaValor = MitadDeLaUltimaCifra(decimalesValorUnitario);
            return new Rango(
                Redondeo.Truncar((cantidad - mediaCantidad) * (valorUnitario - mediaValor), decimalesImporte),
                Redondeo.HaciaArriba((cantidad + mediaCantidad - Piso) * (valorUnitario + mediaValor - Piso), decimalesImporte));
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// Límites del Importe de un traslado o una retención de un concepto: (Base - 10^-NumDecimalesBase / 2) por la tasa
    /// o cuota, truncado, y (Base + 10^-NumDecimalesBase / 2 - 10^-12) por la tasa o cuota, redondeado hacia arriba;
    /// los dos a los decimales del Importe del impuesto.
    /// </summary>
    public static Rango? ImporteImpuesto(decimal baseImpuesto, int decimalesBase, decimal tasaOCuota, int decimalesImporte)
    {
        try
        {
            var media = MitadDeLaUltimaCifra(decimalesBase);
            return new Rango(
                Redondeo.Truncar((baseImpuesto - media) * tasaOCuota, decimalesImporte),
                Redondeo.HaciaArriba((baseImpuesto + media - Piso) * tasaOCuota, decimalesImporte));
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    /// <summary>10 elevado a la menos <paramref name="decimales"/>, entre dos: 0.5 sin decimales, 0.005 con dos.</summary>
    private static decimal MitadDeLaUltimaCifra(int decimales)
    {
        var valor = 0.5m;
        for (var i = 0; i < decimales; i++)
        {
            valor /= 10m;
        }

        return valor;
    }
}
