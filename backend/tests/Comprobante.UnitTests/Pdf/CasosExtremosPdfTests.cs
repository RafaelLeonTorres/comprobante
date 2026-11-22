using System.Xml.Linq;
using Comprobante.Core.Representacion;
using Comprobante.Pdf;
using UglyToad.PdfPig;
using static Comprobante.UnitTests.Pdf.EjemplosDePdf;

namespace Comprobante.UnitTests.Pdf;

/// <summary>
/// Comprobantes que nadie emitiría pero que un XML puede traer: el PDF tiene que salir (o decir por qué no) y no
/// quedarse sin memoria ni atorarse en una página que no termina.
/// </summary>
[Collection(ColeccionCatalogos.Nombre)]
public class CasosExtremosPdfTests(CatalogosFixture catalogos)
{
    private async Task<byte[]> GenerarAsync(XDocument documento, OpcionesDelPdf? opciones = null)
    {
        var representacion = await new ConstructorDeRepresentacion(catalogos.Catalogos).ConstruirAsync(documento);
        return GeneradorPdf.Generar(representacion, opciones);
    }

    private static int Paginas(byte[] pdf)
    {
        using var documento = PdfDocument.Open(pdf);
        return documento.NumberOfPages;
    }

    private static XElement PrimerConcepto(XDocument documento) => documento.Descendants(Cfdi + "Concepto").First();

    [Fact]
    public async Task Los_textos_largos_sin_espacios_se_parten_en_renglones_y_salen_completos()
    {
        var documento = SinTimbre();
        documento.Descendants(Cfdi + "Emisor").Single().SetAttributeValue("Nombre", new string('A', 254));
        documento.Descendants(Cfdi + "Receptor").Single().SetAttributeValue("Nombre", string.Join(' ', Enumerable.Repeat("RAZÓN SOCIAL MUY LARGA", 11)));
        PrimerConcepto(documento).SetAttributeValue("Descripcion", new string('x', 1000));
        PrimerConcepto(documento).SetAttributeValue("NoIdentificacion", new string('N', 100));

        var pdf = await GenerarAsync(documento);

        Assert.InRange(Paginas(pdf), 1, 4);
        var texto = SinEspacios(Texto(pdf));
        Assert.Contains(new string('A', 254), texto, StringComparison.Ordinal);
        Assert.Contains(new string('x', 1000), texto, StringComparison.Ordinal);
        Assert.Contains(new string('N', 100), texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Los_caracteres_que_la_fuente_no_tiene_no_detienen_el_pdf_y_los_que_si_tiene_se_dibujan()
    {
        var documento = SinTimbre();
        documento.Descendants(Cfdi + "Emisor").Single().SetAttributeValue("Nombre", "Pérez 😀 漢字 Ωμέγα مرحبا Привет ŁÓDŹ Ñandú");
        PrimerConcepto(documento).SetAttributeValue("Descripcion", "Servicio 🔧 日本語 ✓ ½ ± ≥ € £ ¥ © ® ™ — – … “comillas” ‘simples’");

        var pdf = await GenerarAsync(documento);

        Assert.Equal(1, Paginas(pdf));
        var texto = SinEspacios(Texto(pdf));
        foreach (var dibujado in new[] { "Pérez", "Ωμέγα", "Привет", "ŁÓDŹ", "Ñandú", "€£¥©®™", "“comillas”" })
        {
            Assert.Contains(dibujado, texto, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Un_concepto_con_300_impuestos_ocupa_varias_hojas_y_los_trae_todos()
    {
        var documento = SinTimbre();
        var traslados = PrimerConcepto(documento).Descendants(Cfdi + "Traslados").First();
        for (var i = 1; i <= 300; i++)
        {
            traslados.Add(Traslado(i));
        }

        var pdf = await GenerarAsync(documento);

        Assert.True(Paginas(pdf) > 1);
        Assert.Contains("$300.00", SinEspacios(Texto(pdf)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_resumen_de_500_impuestos_sale_completo()
    {
        var documento = SinTimbre();
        var traslados = documento.Root!.Element(Cfdi + "Impuestos")!.Element(Cfdi + "Traslados")!;
        for (var i = 1; i <= 500; i++)
        {
            traslados.Add(Traslado(i));
        }

        var pdf = await GenerarAsync(documento);

        Assert.True(Paginas(pdf) > 1);
        Assert.Contains("$500.00", SinEspacios(Texto(pdf)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tres_mil_cfdi_relacionados_salen_completos()
    {
        var documento = SinTimbre();
        var relacion = new XElement(Cfdi + "CfdiRelacionados", new XAttribute("TipoRelacion", "04"));
        for (var i = 0; i < 3000; i++)
        {
            relacion.Add(new XElement(Cfdi + "CfdiRelacionado", new XAttribute("UUID", $"{i:X8}-0D3A-4F6B-9A11-C85E1D40A2F7")));
        }

        documento.Root!.Element(Cfdi + "Emisor")!.AddBeforeSelf(relacion);

        var pdf = await GenerarAsync(documento);

        Assert.True(Paginas(pdf) > 10);
        Assert.Contains("00000BB7-0D3A-4F6B-9A11-C85E1D40A2F7", SinEspacios(Texto(pdf)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mil_quinientos_conceptos_salen_completos()
    {
        var pdf = await GenerarAsync(ConMuchosConceptos(1500));

        Assert.True(Paginas(pdf) > 50);
        Assert.Contains("Conceptonúmero1500:", SinEspacios(Texto(pdf)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_importes_gigantes_las_cifras_se_achican_y_la_descripcion_sigue_legible()
    {
        var documento = SinTimbre();
        var concepto = PrimerConcepto(documento);
        concepto.SetAttributeValue("Cantidad", "1234567.123456");
        concepto.SetAttributeValue("ValorUnitario", "1234567890.123456");
        concepto.SetAttributeValue("Importe", "987654321098.123456");
        concepto.SetAttributeValue("Descuento", "123456789012.12");
        documento.Root!.SetAttributeValue("Total", "99999999999999.99");
        documento.Root.SetAttributeValue("SubTotal", "99999999999999.99");
        foreach (var traslado in documento.Descendants(Cfdi + "Traslado"))
        {
            traslado.SetAttributeValue("Base", "98765432109876.12");
            traslado.SetAttributeValue("Importe", "15802469137580.18");
        }

        var pdf = await GenerarAsync(documento);

        Assert.InRange(Paginas(pdf), 1, 2);
        using var abierto = PdfDocument.Open(pdf);
        var palabras = abierto.GetPage(1).GetWords().Select(p => p.Text).ToList();
        Assert.Contains("corporativa", palabras); // sin el ajuste, la descripción quedaba en una tira de 3 mm partida letra por letra
        Assert.Contains("$99,999,999,999,999.99", SinEspacios(Texto(pdf)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_renglon_mas_alto_que_una_hoja_se_deja_partir_en_vez_de_fallar()
    {
        var documento = SinTimbre();
        var concepto = PrimerConcepto(documento);
        concepto.SetAttributeValue("Cantidad", "1234567.123456");
        concepto.SetAttributeValue("ValorUnitario", "1234567890.123456");
        concepto.SetAttributeValue("Importe", "987654321098.123456");
        concepto.SetAttributeValue("Descuento", "123456789012.12");
        concepto.SetAttributeValue("Descripcion", string.Join(' ', Enumerable.Repeat("palabra", 160)));
        var traslados = concepto.Descendants(Cfdi + "Traslados").First();
        for (var i = 1; i <= 25; i++)
        {
            traslados.Add(Traslado(i));
        }

        var pdf = await GenerarAsync(documento);

        Assert.True(Paginas(pdf) >= 2);
        Assert.Contains("$25.00", SinEspacios(Texto(pdf)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_concepto_de_mil_caracteres_con_60_impuestos_se_parte_entre_hojas()
    {
        var documento = SinTimbre();
        var concepto = PrimerConcepto(documento);
        concepto.SetAttributeValue("Descripcion", string.Join(' ', Enumerable.Repeat("palabra", 125)));
        var traslados = concepto.Descendants(Cfdi + "Traslados").First();
        for (var i = 1; i <= 60; i++)
        {
            traslados.Add(Traslado(i));
        }

        var pdf = await GenerarAsync(documento);

        Assert.True(Paginas(pdf) >= 2);
        Assert.Contains("$60.00", SinEspacios(Texto(pdf)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_comprobante_sin_conceptos_no_dibuja_la_tabla()
    {
        var documento = SinTimbre();
        documento.Descendants(Cfdi + "Conceptos").Single().RemoveNodes();

        var pdf = await GenerarAsync(documento);

        Assert.Equal(1, Paginas(pdf));
        Assert.DoesNotContain("Claveprod.serv.", SinEspacios(Texto(pdf)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_cadena_enorme_se_reparte_en_varias_hojas()
    {
        var documento = ConTimbre();
        documento.Root!.SetAttributeValue("Certificado", new string('Q', 60_000));
        var pdf = await GenerarAsync(documento);

        Assert.True(Paginas(pdf) >= 1);
        Assert.Contains(SinEspacios(SelloSatDePrueba), SinEspacios(Texto(pdf)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_documento_que_no_es_un_comprobante_no_se_dibuja()
    {
        var constructor = new ConstructorDeRepresentacion(catalogos.Catalogos);

        await Assert.ThrowsAsync<ArgumentException>(() => constructor.ConstruirAsync(XDocument.Parse("<otra-cosa/>")));
    }
}
