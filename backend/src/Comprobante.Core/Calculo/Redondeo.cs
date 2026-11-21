namespace Comprobante.Core.Calculo;

/// <summary>
/// Los redondeos del Anexo 20. Todo se calcula con <see cref="decimal"/>: con double, 0.1 + 0.2 no es 0.3 y los
/// centavos se pierden al sumar muchos conceptos.
/// </summary>
public static class Redondeo
{
    /// <summary>Los campos de tipo importe (Cantidad, ValorUnitario, Importe, Descuento, Base...) admiten hasta seis decimales.</summary>
    public const int DecimalesMaximos = 6;

    /// <summary>Redondeo aritmético: el 5 sube (924.225 pasa a 924.23). Es el que usa el SAT en totales y resúmenes.</summary>
    public static decimal Redondear(decimal valor, int decimales) =>
        Math.Round(valor, decimales, MidpointRounding.AwayFromZero);

    /// <summary>Corta los decimales que sobran sin redondear (924.229 pasa a 924.22). Pensado para valores positivos.</summary>
    public static decimal Truncar(decimal valor, int decimales) =>
        Math.Round(valor, decimales, MidpointRounding.ToZero);

    /// <summary>Sube al siguiente valor con esos decimales si sobra algo (924.221 pasa a 924.23; 924.22 no cambia).</summary>
    public static decimal HaciaArriba(decimal valor, int decimales) =>
        Math.Round(valor, decimales, MidpointRounding.ToPositiveInfinity);

    /// <summary>Cuántos decimales tiene el valor sin contar los ceros de la derecha (1.50 tiene uno).</summary>
    public static int Decimales(decimal valor)
    {
        for (var n = 0; n < 28; n++)
        {
            if (Math.Round(valor, n) == valor)
            {
                return n;
            }
        }

        return 28;
    }

    /// <summary>Cuántos decimales trae escrito el texto, ceros incluidos ("3.10" tiene dos y "3" ninguno): así los cuenta el SAT.</summary>
    public static int DecimalesEscritos(string texto)
    {
        var punto = texto.IndexOf('.');
        return punto < 0 ? 0 : texto.Length - punto - 1;
    }
}
