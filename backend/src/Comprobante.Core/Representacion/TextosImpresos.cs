using System.Globalization;
using System.Text;

namespace Comprobante.Core.Representacion;

/// <summary>Cómo se escriben los números y las claves en la representación impresa (comas de miles, símbolo de moneda, porcentajes).</summary>
internal static class TextosImpresos
{
    /// <summary>Lo que se imprime donde el comprobante no trae el dato.</summary>
    public const string Vacio = "—";

    /// <summary>
    /// El número con comas de miles, conservando los decimales tal como están escritos en el XML ("3864.22827" es
    /// "3,864.22827" y "850.00" es "850.00"). Lo que no es un número se devuelve sin tocar.
    /// </summary>
    public static string Agrupar(string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return Vacio;
        }

        var texto = numero.Trim();
        var negativo = texto.StartsWith('-');
        var cuerpo = negativo ? texto[1..] : texto;
        var punto = cuerpo.IndexOf('.', StringComparison.Ordinal);
        var entera = punto < 0 ? cuerpo : cuerpo[..punto];
        var fraccion = punto < 0 ? string.Empty : cuerpo[punto..];
        if (entera.Length == 0 || !entera.All(char.IsAsciiDigit) || !fraccion.Skip(1).All(char.IsAsciiDigit))
        {
            return texto;
        }

        var resultado = new StringBuilder(entera.Length + (entera.Length / 3) + fraccion.Length + 1);
        if (negativo)
        {
            resultado.Append('-');
        }

        for (var i = 0; i < entera.Length; i++)
        {
            if (i > 0 && (entera.Length - i) % 3 == 0)
            {
                resultado.Append(',');
            }

            resultado.Append(entera[i]);
        }

        return resultado.Append(fraccion).ToString();
    }

    /// <summary>
    /// El número con comas de miles y al menos dos decimales, sin perder los que traiga el XML: "10" es "10.00",
    /// "850.5" es "850.50" y "3.141649" sigue siendo "3.141649".
    /// </summary>
    public static string AgruparConDosDecimales(string? numero) =>
        decimal.TryParse(numero, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor)
            ? Agrupar(Formato.Importe(valor, 2))
            : Agrupar(numero);

    /// <summary>El importe con el símbolo del peso, comas de miles y al menos dos decimales: "$1,344.00".</summary>
    public static string Dinero(string? importe) =>
        string.IsNullOrWhiteSpace(importe) ? Vacio : "$" + AgruparConDosDecimales(importe);

    /// <summary>
    /// El importe de un total, con la clave de la moneda cuando no es peso mexicano ("$1,200.00 USD"); sin moneda (XXX) va
    /// solo el importe.
    /// </summary>
    public static string DineroConMoneda(string? importe, string moneda) =>
        moneda is "MXN" or "XXX" or "" ? Dinero(importe) : $"{Dinero(importe)} {moneda}";

    /// <summary>La tasa del XML como porcentaje: "0.160000" es "16.00" y "0.106667" es "10.6667".</summary>
    public static string Porcentaje(string? tasa) =>
        decimal.TryParse(tasa, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor)
            ? Formato.Importe(valor * 100m, 2)
            : Vacio;

    /// <summary>El valor con al menos dos decimales ("0.35"), para las cuotas de los impuestos.</summary>
    public static string DosDecimales(string? numero) =>
        decimal.TryParse(numero, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor)
            ? Formato.Importe(valor, 2)
            : Vacio;

    /// <summary>La clave con la descripción de su catálogo ("G03 - Gastos en general"); sin descripción, solo la clave.</summary>
    public static string Con(string? clave, string? descripcion) =>
        string.IsNullOrWhiteSpace(clave)
            ? Vacio
            : string.IsNullOrWhiteSpace(descripcion) ? clave : $"{clave} - {descripcion}";

    /// <summary>El texto sin los espacios y saltos de línea de los extremos; si no hay nada, el guion largo.</summary>
    public static string OVacio(string? valor) => string.IsNullOrWhiteSpace(valor) ? Vacio : valor.Trim();

    /// <summary>Un sello o certificado sin espacios ni saltos de línea (base64 no los lleva, pero hay PAC que los agrega).</summary>
    public static string SinEspacios(string? valor) =>
        valor is null ? string.Empty : string.Concat(valor.Where(c => !char.IsWhiteSpace(c)));
}
