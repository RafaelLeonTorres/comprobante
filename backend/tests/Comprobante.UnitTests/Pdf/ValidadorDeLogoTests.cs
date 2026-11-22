using System.Buffers.Binary;
using System.Text;
using Comprobante.Pdf;
using static Comprobante.UnitTests.Pdf.EjemplosDePdf;

namespace Comprobante.UnitTests.Pdf;

public class ValidadorDeLogoTests
{
    [Theory]
    [InlineData("logo-prueba.png", "image/png", 600, 200)]
    [InlineData("logo-alto.png", "image/png", 120, 400)]
    [InlineData("logo-prueba.jpg", "image/jpeg", 450, 150)]
    public void Acepta_un_png_o_jpeg_de_verdad_y_dice_su_tipo_y_tamano(string archivo, string tipo, int ancho, int alto)
    {
        var resultado = ValidadorDeLogo.Validar(Logo(archivo));

        Assert.True(resultado.EsValido, resultado.Error);
        Assert.Null(resultado.Error);
        Assert.Equal(tipo, resultado.TipoDeContenido);
        Assert.Equal(ancho, resultado.Ancho);
        Assert.Equal(alto, resultado.Alto);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new byte[0])]
    public void Rechaza_un_archivo_vacio(byte[]? logo)
    {
        var resultado = ValidadorDeLogo.Validar(logo);

        Assert.False(resultado.EsValido);
        Assert.Contains("vacío", resultado.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rechaza_lo_que_pesa_mas_de_200_KB()
    {
        var logo = new byte[ValidadorDeLogo.TamanoMaximoEnBytes + 1];
        PngFalso(10, 10).CopyTo(logo, 0);

        var resultado = ValidadorDeLogo.Validar(logo);

        Assert.False(resultado.EsValido);
        Assert.Contains("200 KB", resultado.Error, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(NoSonPngNiJpeg))]
    public void Rechaza_lo_que_no_es_png_ni_jpeg_aunque_lo_diga_el_nombre(byte[] contenido)
    {
        var resultado = ValidadorDeLogo.Validar(contenido);

        Assert.False(resultado.EsValido);
        Assert.Contains("PNG o JPEG", resultado.Error, StringComparison.Ordinal);
    }

    public static TheoryData<byte[]> NoSonPngNiJpeg() =>
        new()
        {
            Encoding.ASCII.GetBytes("GIF89a\x01\0\x01\0\0\0\0;"),
            Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><image href=\"http://example.com/x.png\"/></svg>"),
            Encoding.ASCII.GetBytes("RIFF\x1a\0\0\0WEBPVP8 "),
            Encoding.UTF8.GetBytes("%PDF-1.7\n1 0 obj<<>>endobj"),
            new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 }, // un ejecutable de Windows
            new byte[] { 0x89, 0x50, 0x4E, 0x47 }, // una firma de PNG cortada
        };

    [Theory]
    [InlineData(2001u, 10u)]
    [InlineData(10u, 2001u)]
    [InlineData(65000u, 65000u)]
    public void Rechaza_un_png_de_mas_de_2000_pixeles_por_lado_sin_decodificarlo(uint ancho, uint alto)
    {
        var resultado = ValidadorDeLogo.Validar(PngFalso(ancho, alto));

        Assert.False(resultado.EsValido);
        Assert.Contains($"{ancho} × {alto} px", resultado.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(3000, 10)]
    [InlineData(10, 2001)]
    public void Rechaza_un_jpeg_de_mas_de_2000_pixeles_por_lado(int ancho, int alto)
    {
        var resultado = ValidadorDeLogo.Validar(JpegFalso(ancho, alto));

        Assert.False(resultado.EsValido);
        Assert.Contains($"{ancho} × {alto} px", resultado.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rechaza_un_png_sin_encabezado_de_imagen()
    {
        var logo = PngFalso(10, 10);
        "XXXX"u8.CopyTo(logo.AsSpan(12)); // donde debía decir IHDR

        var resultado = ValidadorDeLogo.Validar(logo);

        Assert.False(resultado.EsValido);
        Assert.Contains("tamaño", resultado.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Rechaza_un_jpeg_cuyo_tamano_no_se_puede_leer() =>
        Assert.False(ValidadorDeLogo.Validar([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x02, 0xFF, 0xD9]).EsValido);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rechaza_una_imagen_con_encabezado_bueno_y_datos_rotos(bool png)
    {
        var resultado = ValidadorDeLogo.Validar(png ? PngFalso(10, 10) : JpegFalso(10, 10));

        Assert.False(resultado.EsValido);
        Assert.Contains("No se pudo abrir", resultado.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Revisar_solo_mira_el_encabezado_y_Validar_ademas_abre_la_imagen()
    {
        var logo = PngFalso(10, 10);

        Assert.True(ValidadorDeLogo.Revisar(logo).EsValido);
        Assert.False(ValidadorDeLogo.Validar(logo).EsValido);
        Assert.False(ValidadorDeLogo.Revisar(PngFalso(2001, 10)).EsValido);
        Assert.True(ValidadorDeLogo.Validar(Logo("logo-prueba.png")).EsValido);
    }

    /// <summary>La firma de PNG y un bloque IHDR con ese tamaño, y nada más: sirve para probar el encabezado, no se puede dibujar.</summary>
    private static byte[] PngFalso(uint ancho, uint alto)
    {
        var datos = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(datos, 0);
        datos[11] = 13;
        "IHDR"u8.CopyTo(datos.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(datos.AsSpan(16), ancho);
        BinaryPrimitives.WriteUInt32BigEndian(datos.AsSpan(20), alto);
        datos[24] = 8;
        datos[25] = 2;
        return datos;
    }

    /// <summary>El inicio de un JPEG con su bloque de cuadro (SOF0) con ese tamaño y sin datos de imagen.</summary>
    private static byte[] JpegFalso(int ancho, int alto) =>
    [
        0xFF, 0xD8,
        0xFF, 0xC0, 0x00, 0x11, 0x08, (byte)(alto >> 8), (byte)alto, (byte)(ancho >> 8), (byte)ancho,
        0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
        0xFF, 0xD9,
    ];
}
