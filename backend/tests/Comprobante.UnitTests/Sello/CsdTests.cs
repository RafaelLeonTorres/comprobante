using System.Security.Cryptography;
using Comprobante.Core.Sello;
using Comprobante.Core.Xml;

namespace Comprobante.UnitTests.Sello;

public class CsdTests
{
    private static readonly EmisoresDePrueba Emisores = new();

    public static TheoryData<string> Rfcs()
    {
        var datos = new TheoryData<string>();
        foreach (var emisor in new EmisoresDePrueba().Todos)
        {
            datos.Add(emisor.Rfc);
        }

        return datos;
    }

    [Fact]
    public void Son_diez_emisores_cinco_morales_y_cinco_fisicos()
    {
        Assert.Equal(10, Emisores.Todos.Count);
        Assert.Equal(5, Emisores.Todos.Count(e => e.Persona == "moral"));
        Assert.Equal(5, Emisores.Todos.Count(e => e.Persona == "fisica"));
        Assert.Contains(Emisores.Todos, e => e.Rfc == "H&E951128469");
        Assert.Contains(Emisores.Todos, e => e.Rfc == "IAÑL750210963");
    }

    [Theory]
    [MemberData(nameof(Rfcs))]
    public void Cada_CSD_de_prueba_se_lee_y_su_certificado_es_del_RFC_del_emisor(string rfc)
    {
        var csd = Emisores.ObtenerCsd(rfc);

        Assert.NotNull(csd);
        Assert.Equal(rfc, csd.Datos.Rfc);
        Assert.Matches("^[0-9]{20}$", csd.NoCertificado);

        var hoy = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
        Assert.InRange(hoy, csd.Datos.ValidoDesde, csd.Datos.ValidoHasta);
        Assert.False(string.IsNullOrWhiteSpace(csd.Datos.Nombre));
        Assert.True(Convert.FromBase64String(csd.CertificadoBase64).Length > 500);
    }

    [Theory]
    [InlineData("EKU9003173C9", "30001000000500003416")]
    [InlineData("H&E951128469", "30001000000500003431")]
    [InlineData("URE180429TM6", "30001000000500003441")]
    [InlineData("XIA190128J61", "30001000000500003442")]
    [InlineData("L&O950913MSA", "30001000000500003438")]
    [InlineData("WATM640917J45", "30001000000500003362")]
    [InlineData("KICR630120NX3", "30001000000500003358")]
    [InlineData("FUNK671228PH6", "30001000000500003446")]
    [InlineData("IAÑL750210963", "30001000000500003449")]
    [InlineData("MISC491214B86", "30001000000500003359")]
    public void El_NoCertificado_es_el_numero_de_serie_leido_como_texto_ASCII(string rfc, string esperado)
    {
        // Esperados con OpenSSL: openssl x509 -inform DER -noout -serial da 33303030..., que en ASCII es 30001...
        Assert.Equal(esperado, Emisores.ObtenerCsd(rfc)!.NoCertificado);
    }

    [Fact]
    public void Un_RFC_que_no_es_de_prueba_no_tiene_CSD()
    {
        Assert.Null(Emisores.ObtenerCsd("XAXX010101000"));
        Assert.Null(Emisores.ObtenerCsd(null));
        Assert.Null(Emisores.Buscar("XAXX010101000"));
    }

    [Fact]
    public void La_busqueda_por_RFC_no_distingue_mayusculas()
    {
        Assert.Equal("EKU9003173C9", Emisores.Buscar("eku9003173c9")!.Rfc);
        Assert.Equal("H&E951128469", Emisores.Buscar("h&e951128469")!.Rfc);
    }

    [Fact]
    public void El_sello_se_verifica_con_el_certificado_y_no_con_otro_texto_ni_con_otro_certificado()
    {
        var csd = Emisores.ObtenerCsd("EKU9003173C9")!;
        var otro = Emisores.ObtenerCsd("XIA190128J61")!;
        const string cadena = "||4.0|A|123|2026-11-21T09:30:00|01|30001000000500003416|1000.00|MXN|1160.00|I|01|PUE|42501||";

        var sello = csd.Firmar(cadena);

        using var certificado = Csd.LeerCertificado(csd.CertificadoBase64)!;
        using var certificadoOtro = Csd.LeerCertificado(otro.CertificadoBase64)!;
        Assert.True(Csd.Verificar(certificado, cadena, sello));
        Assert.False(Csd.Verificar(certificado, cadena + "x", sello));
        Assert.False(Csd.Verificar(certificadoOtro, cadena, sello));
        Assert.False(Csd.Verificar(certificado, cadena, "esto-no-es-base64"));
        Assert.False(Csd.Verificar(certificado, cadena, Convert.ToBase64String(new byte[10])));
    }

    [Fact]
    public void La_firma_PKCS1_es_determinista_y_de_256_bytes()
    {
        var csd = Emisores.ObtenerCsd("EKU9003173C9")!;

        var uno = csd.Firmar("||texto con acentos: áéíóú ñ y & ||");
        var dos = csd.Firmar("||texto con acentos: áéíóú ñ y & ||");

        Assert.Equal(uno, dos);
        Assert.Equal(256, Convert.FromBase64String(uno).Length);
    }

    [Fact]
    public void Una_llave_que_no_es_del_certificado_se_rechaza()
    {
        var cer = RecursosSat.Bytes("sat/csd-prueba/eku9003173c9.cer");
        var llaveAjena = RecursosSat.Bytes("sat/csd-prueba/xia190128j61.key");

        var error = Assert.Throws<CryptographicException>(() => Csd.Cargar(cer, llaveAjena, "12345678a"));
        Assert.Contains("no corresponde", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_contrasena_equivocada_se_rechaza()
    {
        var cer = RecursosSat.Bytes("sat/csd-prueba/eku9003173c9.cer");
        var llave = RecursosSat.Bytes("sat/csd-prueba/eku9003173c9.key");

        Assert.ThrowsAny<CryptographicException>(() => Csd.Cargar(cer, llave, "otra-contrasena"));
    }

    [Fact]
    public void Un_certificado_que_no_es_X509_no_se_lee()
    {
        Assert.Null(Csd.LeerCertificado("no es base64!"));
        Assert.Null(Csd.LeerCertificado(Convert.ToBase64String("esto no es un certificado"u8.ToArray())));
    }
}
