using System.Globalization;

namespace Comprobante.Core.Representacion;

/// <summary>
/// El importe con letra de la representación impresa: "DIEZ MIL SETECIENTOS OCHO PESOS 48/100 M.N.". Los números van en
/// la forma que se usa delante de un sustantivo (UN peso, VEINTIÚN pesos, CIENTO UN pesos, MIL un pesos) y el redondeo
/// de los centavos sube el cinco, como el del resto de la aplicación.
/// </summary>
public static class ImporteConLetra
{
    /// <summary>El tope: el importe de un CFDI tiene hasta 18 dígitos enteros.</summary>
    private const long Tope = 1_000_000_000_000_000_000;

    private static readonly string[] Hasta29 =
    [
        "CERO", "UN", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
        "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE",
        "VEINTE", "VEINTIÚN", "VEINTIDÓS", "VEINTITRÉS", "VEINTICUATRO", "VEINTICINCO", "VEINTISÉIS", "VEINTISIETE",
        "VEINTIOCHO", "VEINTINUEVE",
    ];

    // El índice es la cifra de las decenas; de 0 a 2 no se usan (se resuelven con Hasta29).
    private static readonly string[] Decenas =
        ["", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"];

    private static readonly string[] Centenas =
    [
        "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS",
        "OCHOCIENTOS", "NOVECIENTOS",
    ];

    /// <summary>
    /// El importe en letra con la moneda y los centavos. El peso mexicano termina en "M.N."; el dólar y el euro llevan su
    /// nombre y su clave; con cualquier otra moneda va el número, los centavos y la clave ("CINCO MIL 00/100 GBP").
    /// </summary>
    /// <param name="importe">Un importe de cero a menos de 10^18; los centavos se redondean (el cinco sube).</param>
    /// <param name="claveMoneda">La clave del catálogo c_Moneda (MXN, USD, EUR...).</param>
    public static string De(decimal importe, string claveMoneda)
    {
        ArgumentNullException.ThrowIfNull(claveMoneda);
        if (importe < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(importe), importe, "El importe no puede ser negativo.");
        }

        var redondeado = decimal.Round(importe, 2, MidpointRounding.AwayFromZero);
        if (redondeado >= Tope)
        {
            throw new ArgumentOutOfRangeException(nameof(importe), importe, "El importe es demasiado grande para escribirlo con letra.");
        }

        var entero = (long)decimal.Truncate(redondeado);
        var centavos = (int)((redondeado - entero) * 100m);

        var (singular, plural, sufijo) = Moneda(claveMoneda);
        var texto = Numero(entero);
        if (singular is not null)
        {
            // "UN MILLÓN DE PESOS", pero "UN MILLÓN DOSCIENTOS MIL PESOS".
            var exactoEnMillones = entero >= 1_000_000 && entero % 1_000_000 == 0;
            texto += (exactoEnMillones ? " DE " : " ") + (entero == 1 ? singular : plural);
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{texto} {centavos:00}/100 {sufijo}");
    }

    private static (string? Singular, string? Plural, string Sufijo) Moneda(string clave) =>
        clave.ToUpperInvariant() switch
        {
            "MXN" => ("PESO", "PESOS", "M.N."),
            "USD" => ("DÓLAR", "DÓLARES", "USD"),
            "EUR" => ("EURO", "EUROS", "EUR"),
            var otra => (null, null, otra),
        };

    /// <summary>El número entero en letra, sin la moneda: 0 es "CERO" y 1 es "UN".</summary>
    internal static string Numero(long n)
    {
        if (n == 0)
        {
            return "CERO";
        }

        var partes = new List<string>(3);
        var billones = (int)(n / 1_000_000_000_000);
        var millones = (int)(n / 1_000_000 % 1_000_000);
        var unidades = (int)(n % 1_000_000);

        if (billones > 0)
        {
            partes.Add(billones == 1 ? "UN BILLÓN" : $"{Grupo(billones)} BILLONES");
        }

        if (millones > 0)
        {
            partes.Add(millones == 1 ? "UN MILLÓN" : $"{Grupo(millones)} MILLONES");
        }

        if (unidades > 0)
        {
            partes.Add(Grupo(unidades));
        }

        return string.Join(' ', partes);
    }

    /// <summary>De 1 a 999,999: los miles y el resto. El mil solo no lleva "UN" (MIL, no UN MIL).</summary>
    private static string Grupo(int n)
    {
        var miles = n / 1000;
        var resto = n % 1000;
        var partes = new List<string>(2);
        if (miles > 0)
        {
            partes.Add(miles == 1 ? "MIL" : $"{MenorAMil(miles)} MIL");
        }

        if (resto > 0)
        {
            partes.Add(MenorAMil(resto));
        }

        return string.Join(' ', partes);
    }

    private static string MenorAMil(int n)
    {
        if (n == 100)
        {
            return "CIEN";
        }

        var centenas = n / 100;
        var resto = n % 100;
        var partes = new List<string>(2);
        if (centenas > 0)
        {
            partes.Add(Centenas[centenas]);
        }

        if (resto > 0)
        {
            partes.Add(MenorACien(resto));
        }

        return string.Join(' ', partes);
    }

    private static string MenorACien(int n)
    {
        if (n < 30)
        {
            return Hasta29[n];
        }

        var unidad = n % 10;
        return unidad == 0 ? Decenas[n / 10] : $"{Decenas[n / 10]} Y {Hasta29[unidad]}";
    }
}
