using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Comprobante.Core.Validacion;

namespace Comprobante.Core.Sello;

/// <summary>
/// Un Certificado de Sello Digital (CSD): el certificado (<c>.cer</c>) y su llave privada (<c>.key</c>). Sirve para sellar un
/// comprobante: firma su cadena original con RSA y SHA-256 (RSASSA-PKCS1-v1_5), como pide el Anexo 20.
/// </summary>
public sealed class Csd : IDisposable
{
    private readonly X509Certificate2 _certificado;
    private readonly RSA _llave;

    // RSA admite firmar desde varios hilos, pero con la llave importada de PKCS#8 no se asume: un candado es barato.
    private readonly object _candado = new();

    private Csd(X509Certificate2 certificado, RSA llave)
    {
        _certificado = certificado;
        _llave = llave;
        Datos = DatosDe(certificado);
        CertificadoBase64 = Convert.ToBase64String(certificado.RawData);
    }

    /// <summary>Los datos del certificado: NoCertificado, RFC, nombre y vigencia.</summary>
    public DatosCertificado Datos { get; }

    public string NoCertificado => Datos.NoCertificado;

    /// <summary>El <c>.cer</c> en Base64, que va en el atributo Certificado.</summary>
    public string CertificadoBase64 { get; }

    /// <summary>Lee el <c>.cer</c> (DER) y el <c>.key</c> (PKCS#8 cifrado con la contraseña) de un CSD.</summary>
    public static Csd Cargar(byte[] cer, byte[] key, string contrasena)
    {
        var certificado = X509CertificateLoader.LoadCertificate(cer);
        var llave = RSA.Create();
        try
        {
            llave.ImportEncryptedPkcs8PrivateKey(contrasena, key, out _);

            // La llave tiene que ser la del certificado: si no, el sello nunca verificaría.
            using var publica = certificado.GetRSAPublicKey()
                ?? throw new CryptographicException("El certificado no es de una llave RSA.");
            if (!publica.ExportParameters(false).Modulus!.AsSpan().SequenceEqual(llave.ExportParameters(false).Modulus))
            {
                throw new CryptographicException("La llave privada no corresponde al certificado.");
            }

            return new Csd(certificado, llave);
        }
        catch
        {
            llave.Dispose();
            certificado.Dispose();
            throw;
        }
    }

    /// <summary>El sello: la firma de la cadena original (UTF-8) en Base64.</summary>
    public string Firmar(string cadenaOriginal)
    {
        var datos = Encoding.UTF8.GetBytes(cadenaOriginal);
        lock (_candado)
        {
            return Convert.ToBase64String(_llave.SignData(datos, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
    }

    public void Dispose()
    {
        _llave.Dispose();
        _certificado.Dispose();
    }

    /// <summary>
    /// El NoCertificado: el número de serie del certificado, que en un CSD son 20 caracteres de texto guardados como bytes
    /// ASCII (<c>3330303031...</c> es <c>30001...</c>).
    /// </summary>
    internal static string NoCertificadoDe(X509Certificate2 certificado) =>
        Encoding.ASCII.GetString(certificado.SerialNumberBytes.Span);

    internal static DatosCertificado DatosDe(X509Certificate2 certificado) => new(
        NoCertificadoDe(certificado),
        RfcDe(certificado),
        certificado.GetNameInfo(X509NameType.SimpleName, forIssuer: false),
        new DateTimeOffset(certificado.NotBefore),
        new DateTimeOffset(certificado.NotAfter));

    /// <summary>
    /// El RFC del titular: en los CSD va en el campo x500UniqueIdentifier del sujeto, como "RFC / CURP del representante"
    /// (<c>EKU9003173C9 / VADA800927DJ3</c>); en el de una persona física, solo el RFC.
    /// </summary>
    private static string? RfcDe(X509Certificate2 certificado)
    {
        foreach (var rdn in certificado.SubjectName.EnumerateRelativeDistinguishedNames())
        {
            if (rdn.GetSingleElementType().Value == "2.5.4.45")
            {
                var texto = rdn.GetSingleElementValue();
                var corte = texto?.IndexOf('/', StringComparison.Ordinal) ?? -1;
                return (corte >= 0 ? texto![..corte] : texto)?.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Comprueba un sello con la llave pública de un certificado. Falso si no verifica, o si el certificado o el sello no se
    /// pueden leer (no hay sello que valga).
    /// </summary>
    public static bool Verificar(X509Certificate2 certificado, string cadenaOriginal, string selloBase64)
    {
        try
        {
            using var publica = certificado.GetRSAPublicKey();
            return publica is not null
                && publica.VerifyData(
                    Encoding.UTF8.GetBytes(cadenaOriginal),
                    Convert.FromBase64String(selloBase64),
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    /// <summary>El certificado de un atributo Certificado (Base64 del .cer), o nulo si no es un certificado X.509 válido.</summary>
    public static X509Certificate2? LeerCertificado(string base64)
    {
        try
        {
            return X509CertificateLoader.LoadCertificate(Convert.FromBase64String(base64));
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return null;
        }
    }
}
