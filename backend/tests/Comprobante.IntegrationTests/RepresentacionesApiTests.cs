using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Comprobante.IntegrationTests;

[Collection(ColeccionApi.Nombre)]
public class RepresentacionesApiTests(ApiFactory api)
{
    private const string Ruta = "/api/representaciones/pdf";
    private static readonly XNamespace Cfdi = "http://www.sat.gob.mx/cfd/4";

    /// <summary>Un PNG de un píxel: lo más chico que el motor de PDF puede decodificar.</summary>
    internal static readonly byte[] PngDeUnPixel = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private readonly HttpClient _http = api.CreateClient();

    private static object Ingreso() => new
    {
        serie = "A",
        folio = "1",
        fecha = "2026-11-21T09:30:00",
        formaPago = "01",
        metodoPago = "PUE",
        emisor = new { rfc = "EKU9003173C9" },
        receptor = new
        {
            rfc = "XIA190128J61",
            nombre = "XENON INDUSTRIAL ARTICLES",
            domicilioFiscalReceptor = "76343",
            regimenFiscalReceptor = "622",
            usoCfdi = "G03",
        },
        conceptos = new[]
        {
            new
            {
                claveProdServ = "84111506",
                claveUnidad = "E48",
                unidad = "Unidad de servicio",
                descripcion = "Servicio de facturación electrónica",
                cantidad = 1,
                valorUnitario = 1000.00m,
                traslados = new[] { new { impuesto = "002", tipoFactor = "Tasa", tasaOCuota = 0.16m } },
            },
        },
    };

    /// <summary>Un CFDI sellado, tal como lo genera la propia app.</summary>
    internal async Task<string> GenerarXmlAsync()
    {
        var respuesta = await _http.PostAsJsonAsync("/api/comprobantes", Ingreso());
        respuesta.EnsureSuccessStatusCode();
        var resultado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return resultado.GetProperty("xml").GetString()!;
    }

    /// <summary>El mismo CFDI con <paramref name="cuantos"/> conceptos (el sello ya no corresponde, al PDF no le importa).</summary>
    internal async Task<string> GenerarXmlConConceptosAsync(int cuantos)
    {
        var documento = XDocument.Parse(await GenerarXmlAsync());
        var conceptos = documento.Descendants(Cfdi + "Conceptos").Single();
        var modelo = conceptos.Elements(Cfdi + "Concepto").Single();
        for (var i = 2; i <= cuantos; i++)
        {
            var copia = new XElement(modelo);
            copia.SetAttributeValue("Descripcion", $"Concepto de muestra número {i}");
            conceptos.Add(copia);
        }

        return documento.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>El formulario del PDF: el XML como archivo (o como texto) y, si se piden, el logo y la opción de los sellos.</summary>
    internal static MultipartFormDataContent Formulario(
        string? xml, byte[]? logo = null, string? incluirCadenaYSellos = null, bool xmlComoTexto = false, string tipoDelLogo = "image/png")
    {
        var formulario = new MultipartFormDataContent();
        if (xml is not null)
        {
            if (xmlComoTexto)
            {
                formulario.Add(new StringContent(xml, new UTF8Encoding(false)), "xml");
            }
            else
            {
                var archivo = new ByteArrayContent(new UTF8Encoding(false).GetBytes(xml));
                archivo.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
                formulario.Add(archivo, "xml", "comprobante.xml");
            }
        }

        if (logo is not null)
        {
            var archivo = new ByteArrayContent(logo);
            archivo.Headers.ContentType = new MediaTypeHeaderValue(tipoDelLogo);
            formulario.Add(archivo, "logo", "logo.png");
        }

        if (incluirCadenaYSellos is not null)
        {
            formulario.Add(new StringContent(incluirCadenaYSellos), "incluirCadenaYSellos");
        }

        return formulario;
    }

    private static async Task<JsonElement> ErroresAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Hay datos que corregir", problema.GetProperty("title").GetString());
        return problema.GetProperty("errors");
    }

    private static void EsUnPdf(byte[] contenido)
    {
        Assert.True(contenido.AsSpan().StartsWith("%PDF-"u8), "La respuesta no empieza como un PDF.");
        Assert.Contains("%%EOF", Encoding.Latin1.GetString(contenido, contenido.Length - 32, 32), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_cfdi_generado_por_la_app_se_descarga_como_pdf()
    {
        using var formulario = Formulario(await GenerarXmlAsync());

        using var respuesta = await _http.PostAsync(Ruta, formulario);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", respuesta.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("EKU9003173C9-A-1-prueba.pdf", respuesta.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.True(respuesta.Headers.CacheControl?.NoStore);
        EsUnPdf(await respuesta.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task El_xml_tambien_puede_ir_como_texto_del_formulario()
    {
        using var formulario = Formulario(await GenerarXmlAsync(), xmlComoTexto: true);

        using var respuesta = await _http.PostAsync(Ruta, formulario);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        EsUnPdf(await respuesta.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Un_logo_png_se_acepta_y_el_pdf_lo_lleva()
    {
        var xml = await GenerarXmlAsync();
        using var sin = await _http.PostAsync(Ruta, Formulario(xml));
        using var con = await _http.PostAsync(Ruta, Formulario(xml, PngDeUnPixel));

        Assert.Equal(HttpStatusCode.OK, con.StatusCode);
        var pdf = await con.Content.ReadAsByteArrayAsync();
        EsUnPdf(pdf);
        Assert.NotEqual(await sin.Content.ReadAsByteArrayAsync(), pdf);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("1")]
    public async Task Con_los_sellos_el_pdf_pesa_mas_que_sin_ellos(string incluir)
    {
        var xml = await GenerarXmlAsync();
        using var con = await _http.PostAsync(Ruta, Formulario(xml, incluirCadenaYSellos: incluir));
        using var sin = await _http.PostAsync(Ruta, Formulario(xml, incluirCadenaYSellos: incluir == "1" ? "0" : "false"));

        Assert.Equal(HttpStatusCode.OK, con.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sin.StatusCode);
        Assert.True((await con.Content.ReadAsByteArrayAsync()).Length > (await sin.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task Un_comprobante_de_800_conceptos_se_dibuja_completo()
    {
        using var formulario = Formulario(await GenerarXmlConConceptosAsync(800));

        using var respuesta = await _http.PostAsync(Ruta, formulario);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        EsUnPdf(await respuesta.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Sin_xml_responde_400_en_el_campo_xml()
    {
        using var formulario = Formulario(null, PngDeUnPixel);

        using var respuesta = await _http.PostAsync(Ruta, formulario);

        var errores = await ErroresAsync(respuesta);
        Assert.Contains("Falta el XML", errores.GetProperty("xml")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_xml_mal_formado_responde_400_con_el_motivo_en_espanol()
    {
        using var formulario = Formulario("<cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/4\">\n  <cfdi:Emisor>\n</cfdi:Comprobante>");

        using var respuesta = await _http.PostAsync(Ruta, formulario);

        var errores = await ErroresAsync(respuesta);
        var mensaje = errores.GetProperty("xml")[0].GetString();
        Assert.StartsWith("El XML no se pudo leer (línea 3)", mensaje, StringComparison.Ordinal);
        Assert.DoesNotContain("expected", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<otra-cosa/>")]
    [InlineData("<cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/3\" Version=\"3.3\"/>")]
    [InlineData("<cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/4\" Version=\"3.3\"/>")]
    public async Task Un_xml_que_no_es_un_cfdi_40_responde_400(string xml)
    {
        using var formulario = Formulario(xml);

        using var respuesta = await _http.PostAsync(Ruta, formulario);

        var errores = await ErroresAsync(respuesta);
        Assert.Contains("no es un CFDI 4.0", errores.GetProperty("xml")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_xml_con_dtd_se_rechaza_sin_resolver_la_entidad()
    {
        const string xml = "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e SYSTEM \"file:///etc/passwd\">]><x>&e;</x>";
        using var formulario = Formulario(xml);

        using var respuesta = await _http.PostAsync(Ruta, formulario);

        var errores = await ErroresAsync(respuesta);
        var mensaje = errores.GetProperty("xml")[0].GetString();
        Assert.Contains("DOCTYPE", mensaje, StringComparison.Ordinal);
        Assert.DoesNotContain("root:", mensaje, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Esto no es una imagen")]
    [InlineData("GIF89a")]
    public async Task Un_logo_que_no_es_png_ni_jpeg_responde_400_en_el_campo_logo(string contenido)
    {
        using var formulario = Formulario(await GenerarXmlAsync(), Encoding.ASCII.GetBytes(contenido));

        using var respuesta = await _http.PostAsync(Ruta, formulario);

        var errores = await ErroresAsync(respuesta);
        Assert.Contains("PNG o JPEG", errores.GetProperty("logo")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_logo_de_mas_de_200_KB_responde_400_en_el_campo_logo()
    {
        var logo = new byte[201 * 1024];
        PngDeUnPixel.CopyTo(logo, 0);
        using var formulario = Formulario(await GenerarXmlAsync(), logo);

        using var respuesta = await _http.PostAsync(Ruta, formulario);

        var errores = await ErroresAsync(respuesta);
        Assert.Contains("200 KB", errores.GetProperty("logo")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_logo_enviado_como_texto_responde_400()
    {
        using var formulario = Formulario(await GenerarXmlAsync());
        formulario.Add(new StringContent("hola"), "logo");

        using var respuesta = await _http.PostAsync(Ruta, formulario);

        var errores = await ErroresAsync(respuesta);
        Assert.Contains("archivo", errores.GetProperty("logo")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_valor_de_incluirCadenaYSellos_que_no_es_booleano_responde_400()
    {
        using var formulario = Formulario(await GenerarXmlAsync(), incluirCadenaYSellos: "quizá");

        using var respuesta = await _http.PostAsync(Ruta, formulario);

        var errores = await ErroresAsync(respuesta);
        Assert.Contains("true o false", errores.GetProperty("incluirCadenaYSellos")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Los_errores_de_varios_campos_llegan_juntos()
    {
        using var formulario = Formulario("<otra-cosa/>", Encoding.ASCII.GetBytes("no soy una imagen"), "quizá");

        using var respuesta = await _http.PostAsync(Ruta, formulario);

        var errores = await ErroresAsync(respuesta);
        Assert.Equal(["incluirCadenaYSellos", "logo", "xml"], errores.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("application/xml")]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("application/json")]
    public async Task Lo_que_no_es_multipart_responde_415(string tipo)
    {
        using var contenido = new StringContent("<cfdi:Comprobante/>", Encoding.UTF8, tipo);

        using var respuesta = await _http.PostAsync(Ruta, contenido);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Un_formulario_mal_armado_responde_400()
    {
        using var contenido = new StringContent("esto no trae partes ni boundary de verdad", Encoding.UTF8);
        contenido.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data") { Parameters = { new NameValueHeaderValue("boundary", "frontera") } };

        using var respuesta = await _http.PostAsync(Ruta, contenido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("La solicitud no es válida", problema.GetProperty("title").GetString());
        Assert.Contains("multipart", problema.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Un_xml_de_mas_de_1_MB_responde_413_sin_dibujarse()
    {
        var enorme = "<cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/4\" Version=\"4.0\">" + new string(' ', 1024 * 1024) + "</cfdi:Comprobante>";
        using var formulario = Formulario(enorme);

        using var respuesta = await _http.PostAsync(Ruta, formulario);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("El contenido es demasiado grande", problema.GetProperty("title").GetString());
    }

    [Fact]
    public async Task La_documentacion_describe_el_formulario_y_las_respuestas()
    {
        var documento = await _http.GetFromJsonAsync<JsonElement>("/api/openapi/v1.json");

        var operacion = documento.GetProperty("paths").GetProperty(Ruta).GetProperty("post");
        Assert.True(operacion.GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _), operacion.GetRawText());
        var respuestas = operacion.GetProperty("responses");
        Assert.True(respuestas.GetProperty("200").TryGetProperty("content", out var contenido) && contenido.TryGetProperty("application/pdf", out _), operacion.GetRawText());
        foreach (var codigo in new[] { "400", "413", "415", "422", "429" })
        {
            Assert.True(respuestas.TryGetProperty(codigo, out _), $"Falta documentar el {codigo}.");
        }
    }
}
