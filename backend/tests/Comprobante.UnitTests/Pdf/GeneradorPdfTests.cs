using System.Xml.Linq;
using Comprobante.Core.Representacion;
using Comprobante.Pdf;
using UglyToad.PdfPig;
using ZXing;
using ZXing.Common;
using static Comprobante.UnitTests.Pdf.EjemplosDePdf;

namespace Comprobante.UnitTests.Pdf;

[Collection(ColeccionCatalogos.Nombre)]
public class GeneradorPdfTests(CatalogosFixture catalogos)
{
    public static TheoryData<string> Escenarios() => Referencia.Escenarios();

    private Task<RepresentacionImpresa> ConstruirAsync(XDocument documento) =>
        new ConstructorDeRepresentacion(catalogos.Catalogos).ConstruirAsync(documento);

    [Theory]
    [MemberData(nameof(Escenarios))]
    public async Task Un_comprobante_sin_timbre_cabe_en_una_hoja_y_dice_que_no_tiene_validez(string escenario)
    {
        var representacion = await ConstruirAsync(SinTimbre(escenario));

        var pdf = GeneradorPdf.Generar(representacion);

        Assert.True(pdf.AsSpan().StartsWith("%PDF-"u8));
        using var documento = PdfDocument.Open(pdf);
        Assert.Equal(1, documento.NumberOfPages);
        var texto = SinEspacios(documento.GetPage(1).Text);
        Assert.Contains(SinEspacios("COMPROBANTE DE PRUEBA · NO TIMBRADO · SIN VALIDEZ FISCAL"), texto, StringComparison.Ordinal);
        Assert.Contains("Sintimbrar", texto, StringComparison.Ordinal);
        Assert.Contains(SinEspacios(representacion.Titulo), texto, StringComparison.Ordinal);
        Assert.Contains(SinEspacios(representacion.Totales.Total), texto, StringComparison.Ordinal);
        Assert.Contains(SinEspacios(representacion.Sellos.TituloDeLaCadena), texto, StringComparison.Ordinal);
        Assert.Empty(documento.GetPage(1).GetHyperlinks());
    }

    [Theory]
    [MemberData(nameof(Escenarios))]
    public async Task Todo_lo_que_arma_la_representacion_aparece_en_el_pdf(string escenario)
    {
        var representacion = await ConstruirAsync(ConTimbre(escenario));

        var texto = SinEspacios(Texto(GeneradorPdf.Generar(representacion)));

        var esperados = representacion.Emisor.Datos.Select(d => d.Valor)
            .Concat(representacion.Receptor.Datos.Select(d => d.Valor))
            .Concat(representacion.Datos.Select(d => d.Valor))
            .Concat(representacion.Relacionados.SelectMany(r => r.Uuids.Prepend(r.Tipo)))
            .Concat(representacion.Conceptos.SelectMany(c => new[] { c.ClaveProdServ, c.Descripcion, c.Importe }.Concat(c.Impuestos)))
            .Concat(representacion.Traslados.Concat(representacion.Retenciones).Select(i => i.Impuesto))
            .Concat([
                representacion.Totales.Subtotal,
                representacion.Totales.Total,
                representacion.Totales.ImporteConLetra ?? string.Empty,
                representacion.FolioFiscal ?? string.Empty,
                representacion.SerieFolio ?? string.Empty,
            ]);
        foreach (var esperado in esperados.Where(e => e.Length > 0))
        {
            Assert.Contains(SinEspacios(esperado), texto, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Un_comprobante_timbrado_lleva_uuid_enlace_de_verificacion_y_sellos_sin_la_marca_de_prueba()
    {
        var representacion = await ConstruirAsync(ConTimbre());

        var pdf = GeneradorPdf.Generar(representacion);

        using var documento = PdfDocument.Open(pdf);
        Assert.Equal(1, documento.NumberOfPages);
        var pagina = documento.GetPage(1);
        var texto = SinEspacios(pagina.Text);
        Assert.Contains(UuidDePrueba, texto, StringComparison.Ordinal);
        Assert.Contains(SinEspacios("Cadena original del complemento de certificación digital del SAT"), texto, StringComparison.Ordinal);
        Assert.Contains("Sellodigitaldelemisor", texto, StringComparison.Ordinal);
        Assert.Contains("SellodigitaldelSAT", texto, StringComparison.Ordinal);
        Assert.Contains(SinEspacios(SelloSatDePrueba), texto, StringComparison.Ordinal);
        Assert.DoesNotContain("SINVALIDEZFISCAL", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Sintimbrar", texto, StringComparison.Ordinal);

        var enlaces = pagina.GetHyperlinks();
        Assert.NotEmpty(enlaces);
        Assert.All(enlaces, enlace => Assert.Equal(representacion.Sellos.UrlDeVerificacion, enlace.Uri));
    }

    [Fact]
    public async Task El_qr_trae_la_direccion_de_verificacion_del_sat()
    {
        var representacion = await ConstruirAsync(ConTimbre());
        var url = representacion.Sellos.UrlDeVerificacion;

        Assert.NotNull(url);
        Assert.StartsWith("https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx?id=" + UuidDePrueba + "&re=", url, StringComparison.Ordinal);
        Assert.Equal(url, DecodificarQr(CodigoQr.Modulos(url)));
    }

    [Theory]
    [InlineData("https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx?id=A&re=B&rr=C&tt=0000000001.000000&fe=AAAAAAAA")]
    [InlineData("a")]
    [InlineData("Ñandú: ¿qué tal? 😀")]
    public void El_qr_se_puede_leer_de_vuelta(string texto) => Assert.Equal(texto, DecodificarQr(CodigoQr.Modulos(texto)));

    [Fact]
    public void El_qr_como_svg_es_un_xml_cuadrado_con_el_mismo_numero_de_modulos()
    {
        var modulos = CodigoQr.Modulos("https://example.com/prueba");

        var svg = XDocument.Parse(CodigoQr.ComoSvg("https://example.com/prueba")).Root!;

        Assert.Equal("svg", svg.Name.LocalName);
        Assert.Equal($"0 0 {modulos.GetLength(0)} {modulos.GetLength(0)}", svg.Attribute("viewBox")!.Value);
        Assert.Equal(modulos.GetLength(0), modulos.GetLength(1));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sin_cadena_ni_sellos_se_omiten_los_textos_largos(bool timbrado)
    {
        var representacion = await ConstruirAsync(timbrado ? ConTimbre() : SinTimbre());

        var pdf = GeneradorPdf.Generar(representacion, new OpcionesDelPdf { IncluirCadenaYSellos = false });

        using var documento = PdfDocument.Open(pdf);
        var texto = SinEspacios(documento.GetPage(1).Text);
        Assert.DoesNotContain("Cadenaoriginal", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Sellodigital", texto, StringComparison.Ordinal);
        Assert.DoesNotContain(SinEspacios(representacion.Sellos.Cadena[..30]), texto, StringComparison.Ordinal);
        if (timbrado)
        {
            Assert.NotEmpty(documento.GetPage(1).GetHyperlinks());
        }
    }

    [Fact]
    public async Task El_pdf_lleva_titulo_idioma_y_etiquetas_para_lectores_de_pantalla()
    {
        var representacion = await ConstruirAsync(ConTimbre());

        using var documento = PdfDocument.Open(GeneradorPdf.Generar(representacion));

        Assert.Equal(representacion.TituloDelDocumento, documento.Information.Title);
        Assert.Equal(representacion.AsuntoDelDocumento, documento.Information.Subject);
        var catalogo = documento.Structure.Catalog.CatalogDictionary;
        Assert.True(catalogo.Data.ContainsKey("StructTreeRoot"), "El PDF no está etiquetado.");
        Assert.True(catalogo.Data.ContainsKey("Lang"), "El PDF no declara su idioma.");
        Assert.True(documento.TryGetXmpMetadata(out var xmp));
        var metadatos = xmp!.GetXDocument()!.ToString();
        Assert.Contains("pdfuaid", metadatos, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("logo-prueba.png")]
    [InlineData("logo-prueba.jpg")]
    [InlineData("logo-alto.png")]
    public async Task El_logo_del_usuario_va_como_imagen_dentro_de_su_recuadro(string archivo)
    {
        var representacion = await ConstruirAsync(SinTimbre());

        var pdf = GeneradorPdf.Generar(representacion, new OpcionesDelPdf { Logo = Logo(archivo) });

        using var documento = PdfDocument.Open(pdf);
        Assert.Equal(1, documento.NumberOfPages);
        var imagen = Assert.Single(documento.GetPage(1).GetImages());
        Assert.InRange(imagen.BoundingBox.Width, 1, (60 * 72 / 25.4) + 1); // el recuadro mide 60 × 20 mm
        Assert.InRange(imagen.BoundingBox.Height, 1, (20 * 72 / 25.4) + 1);
    }

    [Fact]
    public async Task Un_comprobante_largo_repite_el_encabezado_de_la_tabla_y_numera_las_paginas()
    {
        var representacion = await ConstruirAsync(ConMuchosConceptos(70));

        using var documento = PdfDocument.Open(GeneradorPdf.Generar(representacion));

        Assert.True(documento.NumberOfPages > 2);
        var hojas = documento.GetPages().Select(pagina => SinEspacios(pagina.Text)).ToList();
        for (var i = 0; i < hojas.Count; i++)
        {
            Assert.Contains($"Página{i + 1}de{hojas.Count}", hojas[i], StringComparison.Ordinal);
        }

        Assert.Contains("Claveprod.serv.", hojas[0], StringComparison.Ordinal);
        Assert.Contains("Claveprod.serv.", hojas[1], StringComparison.Ordinal); // la tabla sigue en la segunda hoja con su encabezado
    }

    [Fact]
    public async Task Varios_pdf_a_la_vez_no_se_mezclan()
    {
        var representaciones = new List<RepresentacionImpresa>();
        foreach (var folio in new[] { "1", "22", "333", "4444", "55", "666", "7777", "88888" })
        {
            var documento = SinTimbre();
            documento.Root!.SetAttributeValue("Serie", "P");
            documento.Root.SetAttributeValue("Folio", folio);
            representaciones.Add(await ConstruirAsync(documento));
        }

        var pdfs = await Task.WhenAll(representaciones.Select(r => Task.Run(() => GeneradorPdf.Generar(r))));

        for (var i = 0; i < pdfs.Length; i++)
        {
            Assert.Contains(SinEspacios(representaciones[i].SerieFolio!), SinEspacios(Texto(pdfs[i])), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void No_dibuja_una_representacion_nula() =>
        Assert.Throws<ArgumentNullException>(() => GeneradorPdf.Generar(null!));

    /// <summary>Lee el QR como lo haría un teléfono: la matriz de módulos, ampliada y con su zona de silencio.</summary>
    private static string? DecodificarQr(bool[,] modulos)
    {
        const int escala = 6;
        const int silencio = 4;
        var lado = modulos.GetLength(0);
        var ancho = (lado + (2 * silencio)) * escala;
        var luminancia = new byte[ancho * ancho];
        Array.Fill(luminancia, (byte)255);
        for (var y = 0; y < lado; y++)
        {
            for (var x = 0; x < lado; x++)
            {
                if (!modulos[y, x])
                {
                    continue;
                }

                for (var dy = 0; dy < escala; dy++)
                {
                    for (var dx = 0; dx < escala; dx++)
                    {
                        luminancia[((((y + silencio) * escala) + dy) * ancho) + ((x + silencio) * escala) + dx] = 0;
                    }
                }
            }
        }

        var lector = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { PossibleFormats = [BarcodeFormat.QR_CODE], TryHarder = true },
        };
        return lector.Decode(new RGBLuminanceSource(luminancia, ancho, ancho, RGBLuminanceSource.BitmapFormat.Gray8))?.Text;
    }
}
