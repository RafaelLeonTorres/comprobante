using System.Xml.Linq;
using Comprobante.Core.Representacion;

namespace Comprobante.UnitTests.Representacion;

[Collection(ColeccionCatalogos.Nombre)]
public class ConstructorDeRepresentacionTests(CatalogosFixture catalogos)
{
    private static readonly XNamespace Cfdi = "http://www.sat.gob.mx/cfd/4";
    private static readonly XNamespace Tfd = "http://www.sat.gob.mx/TimbreFiscalDigital";

    public static TheoryData<string> Escenarios() => Referencia.Escenarios();

    private Task<RepresentacionImpresa> Armar(XDocument documento) =>
        new ConstructorDeRepresentacion(catalogos.Catalogos).ConstruirAsync(documento);

    private Task<RepresentacionImpresa> Armar(string escenario) => Armar(XDocument.Parse(Referencia.Xml(escenario)));

    private static string Dato(IEnumerable<DatoImpreso> datos, string etiqueta) =>
        datos.Single(d => d.Etiqueta == etiqueta).Valor;

    /// <summary>El comprobante de referencia con el timbre que le pondría un PAC (los sellos y el certificado son inventados).</summary>
    private static XDocument ConTimbre(string escenario, out string sello)
    {
        var documento = XDocument.Parse(Referencia.Xml(escenario));
        sello = documento.Root!.Attribute("Sello")!.Value;
        documento.Root.Add(new XElement(
            Cfdi + "Complemento",
            new XElement(
                Tfd + "TimbreFiscalDigital",
                new XAttribute(XNamespace.Xmlns + "tfd", Tfd.NamespaceName),
                new XAttribute("Version", "1.1"),
                new XAttribute("UUID", "7B2C9E54-0D3A-4F6B-9A11-C85E1D40A2F7"),
                new XAttribute("FechaTimbrado", "2026-11-20T18:05:12"),
                new XAttribute("RfcProvCertif", "SPR190613I52"),
                new XAttribute("SelloCFD", sello),
                new XAttribute("NoCertificadoSAT", "30001000000500003456"),
                new XAttribute(
                    "SelloSAT",
                    "dGVzdG8gZGVsIHNlbGxvIGRlbCBTQVQgaW52ZW50YWRvIHBhcmEgbGEgcHJ1ZWJhIGRlIGxhIHBsYW50aWxsYQ=="))));
        return documento;
    }

    [Theory]
    [MemberData(nameof(Escenarios))]
    public async Task Arma_cada_escenario_de_referencia_con_su_cadena_original_y_su_sello(string escenario)
    {
        var representacion = await Armar(escenario);

        Assert.False(representacion.EstaTimbrado);
        Assert.Null(representacion.FolioFiscal);
        Assert.Null(representacion.Sellos.UrlDeVerificacion);
        Assert.Equal("Cadena original del comprobante", representacion.Sellos.TituloDeLaCadena);
        Assert.Equal(Referencia.Cadena(escenario), representacion.Sellos.Cadena);
        var sello = XDocument.Parse(Referencia.Xml(escenario)).Root!.Attribute("Sello")!.Value;
        Assert.Equal(sello, representacion.Sellos.SelloDelEmisor);
        Assert.Null(representacion.Sellos.SelloDelSat);
        Assert.EndsWith("-prueba.pdf", representacion.NombreDeArchivo, StringComparison.Ordinal);
        Assert.Contains("sin validez fiscal", representacion.AsuntoDelDocumento, StringComparison.Ordinal);
        Assert.NotEmpty(representacion.Conceptos);
    }

    [Fact]
    public async Task Escribe_el_comprobante_con_retenciones_como_va_en_el_boceto()
    {
        var r = await Armar("ingreso-retenciones");

        Assert.Equal("FACTURA", r.Titulo);
        Assert.Equal("H-2026-0042", r.SerieFolio);
        Assert.Equal("4.0", r.Version);
        Assert.Equal("WATM640917J45-H-2026-0042-prueba.pdf", r.NombreDeArchivo);
        Assert.Equal("Factura H-2026-0042", r.TituloDelDocumento);

        Assert.Equal("Emisor", r.Emisor.Titulo);
        Assert.Equal("MARIA WATEMBER TORRES", Dato(r.Emisor.Datos, "Nombre"));
        Assert.Equal(Enfasis.Nombre, r.Emisor.Datos.Single(d => d.Etiqueta == "Nombre").Enfasis);
        Assert.Equal("WATM640917J45", Dato(r.Emisor.Datos, "RFC"));
        Assert.Equal(Enfasis.Codigo, r.Emisor.Datos.Single(d => d.Etiqueta == "RFC").Enfasis);
        Assert.Equal("612 - Personas Físicas con Actividades Empresariales y Profesionales", Dato(r.Emisor.Datos, "Régimen fiscal"));
        Assert.Equal("43543", Dato(r.Emisor.Datos, "Lugar de expedición"));

        Assert.Equal("Receptor", r.Receptor.Titulo);
        Assert.Equal("ESCUELA KEMPER URGATE", Dato(r.Receptor.Datos, "Nombre"));
        Assert.Equal("EKU9003173C9", Dato(r.Receptor.Datos, "RFC"));
        Assert.Equal("G03 - Gastos en general.", Dato(r.Receptor.Datos, "Uso del CFDI"));
        Assert.Equal("601 - General de Ley Personas Morales", Dato(r.Receptor.Datos, "Régimen fiscal"));
        Assert.Equal("42501", Dato(r.Receptor.Datos, "Domicilio fiscal"));
        Assert.DoesNotContain(r.Receptor.Datos, d => d.Etiqueta == "Residencia fiscal");

        Assert.Equal("2026-11-19T12:00:00", Dato(r.Datos, "Fecha y hora de emisión"));
        Assert.Equal("I - Ingreso", Dato(r.Datos, "Tipo de comprobante"));
        Assert.Equal("01 - No aplica", Dato(r.Datos, "Exportación"));
        Assert.Equal("MXN - Peso Mexicano", Dato(r.Datos, "Moneda"));
        Assert.Equal("—", Dato(r.Datos, "Tipo de cambio"));
        Assert.Equal("03 - Transferencia electrónica de fondos", Dato(r.Datos, "Forma de pago"));
        Assert.Equal("PUE - Pago en una sola exhibición", Dato(r.Datos, "Método de pago"));
        Assert.Equal("Contado", Dato(r.Datos, "Condiciones de pago"));
        Assert.Equal("No. de serie del certificado del emisor", r.Datos[^1].Etiqueta);
        Assert.Empty(r.Relacionados);

        Assert.Equal(3, r.Conceptos.Count);
        var primero = r.Conceptos[0];
        Assert.Equal("80101500", primero.ClaveProdServ);
        Assert.Equal("HUR · Hora", primero.Unidad);
        Assert.Equal("10.00", primero.Cantidad);
        Assert.Null(primero.NoIdentificacion);
        Assert.Equal("Asesoría en administración corporativa", primero.Descripcion);
        Assert.Equal("850.00", primero.ValorUnitario);
        Assert.Equal("100.00", primero.Descuento);
        Assert.Equal("02", primero.ObjetoImp);
        Assert.Equal("8,500.00", primero.Importe);
        Assert.Equal(
            ["Traslado IVA 16.00 %: $1,344.00", "Retención ISR 10.00 %: $840.00", "Retención IVA 10.6667 %: $896.0028"],
            primero.Impuestos);

        var segundo = r.Conceptos[1];
        Assert.Equal("SW-01", segundo.NoIdentificacion);
        Assert.Equal("1.00", segundo.Cantidad);
        Assert.Null(segundo.Descuento);
        Assert.Equal("2,500.50", segundo.Importe);
        Assert.Equal(["Traslado IVA 0.00 %: $0.00"], segundo.Impuestos);

        Assert.Equal(["Traslado IVA: exento"], r.Conceptos[2].Impuestos);

        Assert.Equal("$11,200.48", r.Totales.Subtotal);
        Assert.Equal("$100.00", r.Totales.Descuento);
        Assert.Equal("$1,344.00", r.Totales.TotalTrasladados);
        Assert.Equal("$1,736.00", r.Totales.TotalRetenidos);
        Assert.Equal("$10,708.48", r.Totales.Total);
        Assert.Equal("DIEZ MIL SETECIENTOS OCHO PESOS 48/100 M.N.", r.Totales.ImporteConLetra);

        Assert.Equal(
            [
                new ImpuestoImpreso("2,500.50", "002 IVA", "Tasa", "0.000000", "$0.00"),
                new ImpuestoImpreso("8,400.00", "002 IVA", "Tasa", "0.160000", "$1,344.00"),
                new ImpuestoImpreso("199.98", "002 IVA", "Exento", null, null),
            ],
            r.Traslados);
        Assert.Equal(
            [
                new ImpuestoImpreso(null, "001 ISR", null, null, "$840.00"),
                new ImpuestoImpreso(null, "002 IVA", null, null, "$896.00"),
            ],
            r.Retenciones);
    }

    [Fact]
    public async Task Con_otra_moneda_los_totales_llevan_su_clave_y_el_importe_con_letra_su_nombre()
    {
        var r = await Armar("ingreso-dolares");

        Assert.Equal("USD - Dólar americano", Dato(r.Datos, "Moneda"));
        Assert.Equal("17.4521", Dato(r.Datos, "Tipo de cambio"));
        Assert.Equal("$3,930.89 USD", r.Totales.Subtotal);
        Assert.Equal("$864.10 USD", r.Totales.Descuento);
        Assert.Equal("$490.69 USD", r.Totales.TotalTrasladados);
        Assert.Null(r.Totales.TotalRetenidos);
        Assert.Equal("$3,557.48 USD", r.Totales.Total);
        Assert.Equal("TRES MIL QUINIENTOS CINCUENTA Y SIETE DÓLARES 48/100 USD", r.Totales.ImporteConLetra);
        Assert.Empty(r.Retenciones);

        // Lo que el XML trae con más decimales se imprime completo.
        var primero = r.Conceptos[0];
        Assert.Equal("3.141649", primero.Cantidad);
        Assert.Equal("1,230.00", primero.ValorUnitario);
        Assert.Equal("3,864.22827", primero.Importe);
        Assert.Equal(["Traslado IVA 16.00 %: $480.020523"], primero.Impuestos);
        Assert.Equal("SKU-9981", r.Conceptos[1].NoIdentificacion);
        Assert.Equal("33.333333", r.Conceptos[1].ValorUnitario);

        Assert.Equal("H&E951128469", Dato(r.Emisor.Datos, "RFC"));
        Assert.Equal("HERRERIA & ELECTRICOS", Dato(r.Emisor.Datos, "Nombre"));
        Assert.Equal("H_E951128469-USD-7-prueba.pdf", r.NombreDeArchivo);
    }

    [Fact]
    public async Task Un_egreso_se_titula_nota_de_credito_y_muestra_los_CFDI_relacionados()
    {
        var r = await Armar("egreso-relacionado");

        Assert.Equal("NOTA DE CRÉDITO", r.Titulo);
        Assert.Equal("E - Egreso", Dato(r.Datos, "Tipo de comprobante"));
        Assert.Equal("Nota de crédito E-45", r.TituloDelDocumento);
        var relacion = Assert.Single(r.Relacionados);
        Assert.Equal("01 - Nota de crédito de los documentos relacionados", relacion.Tipo);
        Assert.Equal(["5FB2822E-396D-4725-8521-CDC4BDD20CCF"], relacion.Uuids);
        Assert.Equal("IANL750210963-E-45-prueba.pdf", r.NombreDeArchivo); // la Ñ del RFC pasa a N
    }

    [Fact]
    public async Task El_publico_en_general_muestra_la_informacion_global_y_se_nombra_con_la_fecha()
    {
        var r = await Armar("publico-en-general");

        Assert.Null(r.SerieFolio);
        Assert.Equal("Factura MISC491214B86", r.TituloDelDocumento);
        Assert.Equal("MISC491214B86-20261121000500-prueba.pdf", r.NombreDeArchivo);
        Assert.Equal("01 - Diario", Dato(r.Datos, "Periodicidad de la información global"));
        Assert.Equal("11 - Noviembre", Dato(r.Datos, "Mes de la información global"));
        Assert.Equal("2026", Dato(r.Datos, "Año de la información global"));
        Assert.Equal("XAXX010101000", Dato(r.Receptor.Datos, "RFC"));
        Assert.Equal("S01 - Sin efectos fiscales.", Dato(r.Receptor.Datos, "Uso del CFDI"));
        Assert.Equal("—", Dato(r.Datos, "Condiciones de pago"));
        Assert.Equal("ACT · Actividad", r.Conceptos[0].Unidad); // sin atributo Unidad, el nombre sale del catálogo
    }

    [Fact]
    public async Task Con_el_timbre_del_SAT_arma_el_QR_la_cadena_del_timbre_y_los_datos_de_certificacion()
    {
        var documento = ConTimbre("ingreso-dolares", out var sello);

        var r = await Armar(documento);

        Assert.True(r.EstaTimbrado);
        Assert.Equal("7B2C9E54-0D3A-4F6B-9A11-C85E1D40A2F7", r.FolioFiscal);
        Assert.Equal("H_E951128469-USD-7.pdf", r.NombreDeArchivo); // ya sin "-prueba"
        Assert.DoesNotContain("sin validez fiscal", r.AsuntoDelDocumento, StringComparison.Ordinal);

        // El RFC con "&" va escapado, y el total con diez enteros y seis decimales.
        var finalDelSello = Uri.EscapeDataString(sello[^8..]);
        Assert.Equal(
            "https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx?id=7B2C9E54-0D3A-4F6B-9A11-C85E1D40A2F7"
            + "&re=H%26E951128469&rr=L%26O950913MSA&tt=0000003557.480000&fe=" + finalDelSello,
            r.Sellos.UrlDeVerificacion);

        Assert.Equal("Cadena original del complemento de certificación digital del SAT", r.Sellos.TituloDeLaCadena);
        Assert.Equal(
            $"||1.1|7B2C9E54-0D3A-4F6B-9A11-C85E1D40A2F7|2026-11-20T18:05:12|SPR190613I52|{sello}|30001000000500003456||",
            r.Sellos.Cadena);
        Assert.Equal(sello, r.Sellos.SelloDelEmisor);
        Assert.StartsWith("dGVzdG8gZGVsIHNlbGxv", r.Sellos.SelloDelSat, StringComparison.Ordinal);

        Assert.Equal("30001000000500003456", Dato(r.Datos, "No. de serie del certificado del SAT"));
        Assert.Equal("2026-11-20T18:05:12", Dato(r.Datos, "Fecha y hora de certificación"));
        Assert.Equal("SPR190613I52", Dato(r.Datos, "RFC del proveedor de certificación"));
        Assert.Equal("RFC del proveedor de certificación", r.Datos[^1].Etiqueta);
    }

    [Fact]
    public async Task Sin_moneda_no_hay_importe_con_letra()
    {
        var documento = XDocument.Parse(Referencia.Xml("ingreso-basico"));
        documento.Root!.SetAttributeValue("Moneda", "XXX");

        var r = await Armar(documento);

        Assert.Equal("$1,160.00", r.Totales.Total); // sin clave: XXX no es una moneda
        Assert.Null(r.Totales.ImporteConLetra);
    }

    [Fact]
    public async Task Una_cuota_de_IEPS_se_escribe_como_cuota()
    {
        var documento = XDocument.Parse(Referencia.Xml("ingreso-basico"));
        var traslado = documento.Descendants(Cfdi + "Concepto").First().Descendants(Cfdi + "Traslado").Single();
        traslado.SetAttributeValue("Impuesto", "003");
        traslado.SetAttributeValue("TipoFactor", "Cuota");
        traslado.SetAttributeValue("TasaOCuota", "0.350000");
        traslado.SetAttributeValue("Importe", "35.00");

        var r = await Armar(documento);

        Assert.Equal(["Traslado IEPS cuota $0.35: $35.00"], r.Conceptos[0].Impuestos);
    }

    [Fact]
    public async Task Una_clave_que_no_esta_en_el_catalogo_se_imprime_sola()
    {
        var documento = XDocument.Parse(Referencia.Xml("ingreso-basico"));
        documento.Root!.SetAttributeValue("FormaPago", "ZZ");
        documento.Descendants(Cfdi + "Concepto").First().Attribute("Unidad")!.Remove();
        documento.Descendants(Cfdi + "Concepto").First().SetAttributeValue("ClaveUnidad", "ZZZ");

        var r = await Armar(documento);

        Assert.Equal("ZZ", Dato(r.Datos, "Forma de pago"));
        Assert.Equal("ZZZ", r.Conceptos[0].Unidad);
    }

    [Fact]
    public async Task Sin_el_atributo_Unidad_usa_el_nombre_de_la_clave_de_unidad()
    {
        var documento = XDocument.Parse(Referencia.Xml("ingreso-basico"));
        documento.Descendants(Cfdi + "Concepto").First().Attribute("Unidad")!.Remove();

        var r = await Armar(documento);

        Assert.Equal("E48 · Unidad de servicio", r.Conceptos[0].Unidad);
    }

    [Fact]
    public async Task Un_documento_que_no_es_un_comprobante_se_rechaza()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Armar(new XDocument(new XElement("otra-cosa"))));
    }

    [Theory]
    [InlineData("10708.48", "0000010708.480000")]
    [InlineData("143", "0000000143.000000")]
    [InlineData("0", "0000000000.000000")]
    [InlineData("1234567890.123456", "1234567890.123456")]
    [InlineData(null, "0000000000.000000")]
    public void El_total_del_QR_lleva_diez_enteros_y_seis_decimales(string? total, string esperado)
    {
        var url = ConstructorDeRepresentacion.UrlDeVerificacion("U", "R", "S", total, "ABCDEFGH");

        Assert.Contains($"&tt={esperado}&", url, StringComparison.Ordinal);
    }

    [Fact]
    public void La_direccion_del_QR_lleva_solo_los_ultimos_ocho_caracteres_del_sello_y_escapa_lo_que_hace_falta()
    {
        var url = ConstructorDeRepresentacion.UrlDeVerificacion("U-1", "A&A010101AA1", "XAXX010101000", "1.5", "xxxxxxxxxxab+/cd==");

        Assert.Equal(
            "https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx?id=U-1&re=A%26A010101AA1&rr=XAXX010101000"
            + "&tt=0000000001.500000&fe=ab%2B%2Fcd%3D%3D",
            url);
    }

    [Theory]
    [InlineData("WATM640917J45", "H", "2026-0042", null, null, false, "WATM640917J45-H-2026-0042-prueba.pdf")]
    [InlineData("WATM640917J45", "H", "2026-0042", "UUID-1", null, true, "WATM640917J45-H-2026-0042.pdf")]
    [InlineData("WATM640917J45", null, "15", null, null, false, "WATM640917J45-15-prueba.pdf")]
    [InlineData("WATM640917J45", "A", null, null, null, false, "WATM640917J45-A-prueba.pdf")]
    [InlineData("WATM640917J45", null, null, "7B2C9E54-0D3A", "2026-11-21T09:30:00", true, "WATM640917J45-7B2C9E54-0D3A.pdf")]
    [InlineData("WATM640917J45", null, null, null, "2026-11-21T09:30:00", false, "WATM640917J45-20261121093000-prueba.pdf")]
    [InlineData("WATM640917J45", null, null, null, null, false, "WATM640917J45-sin-folio-prueba.pdf")]
    [InlineData("IAÑL750210963", "E", "45", null, null, false, "IANL750210963-E-45-prueba.pdf")]
    [InlineData("H&E951128469", "A/B", "1 2", null, null, false, "H_E951128469-A_B-1_2-prueba.pdf")]
    [InlineData(null, null, null, null, null, false, "sin-folio-prueba.pdf")]
    public void El_nombre_del_archivo_no_lleva_caracteres_que_un_sistema_de_archivos_rechace(
        string? rfc, string? serie, string? folio, string? uuid, string? fecha, bool timbrado, string esperado) =>
        Assert.Equal(esperado, ConstructorDeRepresentacion.NombreDeArchivo(rfc, serie, folio, uuid, fecha, timbrado));
}
