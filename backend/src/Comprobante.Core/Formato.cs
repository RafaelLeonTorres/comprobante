using System.Globalization;
using Comprobante.Core.Calculo;

namespace Comprobante.Core;

/// <summary>Cómo se escriben los números en el XML: punto decimal, sin separador de miles y sin notación científica.</summary>
public static class Formato
{
    /// <summary>
    /// Un importe con al menos <paramref name="minimoDecimales"/> decimales y, si hacen falta, hasta seis, sin ceros de más:
    /// 1230 con dos decimales es "1230.00" y 3864.22827 es "3864.22827".
    /// </summary>
    public static string Importe(decimal valor, int minimoDecimales) =>
        Fijo(valor, Math.Max(Redondeo.Decimales(valor), minimoDecimales));

    /// <summary>El valor con exactamente esos decimales (por ejemplo, la tasa siempre con seis: "0.160000").</summary>
    public static string Fijo(decimal valor, int decimales) =>
        valor.ToString("F" + decimales.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
