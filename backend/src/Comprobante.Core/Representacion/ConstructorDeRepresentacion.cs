using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Comprobante.Core.Catalogos;
using Comprobante.Core.Validacion.Reglas;
using Comprobante.Core.Xml;
using static Comprobante.Core.Representacion.TextosImpresos;

namespace Comprobante.Core.Representacion;

/// <summary>
/// Arma la <see cref="RepresentacionImpresa"/> de un CFDI 4.0: lee el XML (ya validado por quien llama), le pone la
/// descripción de cada clave de los catálogos del SAT, escribe los importes, calcula el importe con letra, y si el
/// comprobante trae el timbre del SAT, arma la dirección del código QR y la cadena original del timbre. No dibuja nada.
/// </summary>
/// <param name="catalogos">De dónde salen las descripciones de las claves (los mismos catálogos del generador y el validador).</param>
public sealed partial class ConstructorDeRepresentacion(ICatalogos catalogos)
{
    private const string EspacioDelTimbre = "http://www.sat.gob.mx/TimbreFiscalDigital";

    /// <summary>La página del SAT que verifica un comprobante; el QR de la representación impresa apunta aquí.</summary>
    internal const string DireccionDeVerificacion = "https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx";

    /// <summary>Una clave de catálogo más larga que esto no puede existir: no se manda a la base de datos.</summary>
    private const int LongitudMaximaDeClave = 20;

    private static readonly XNamespace Cfdi = EscritorCfdi.EspacioCfdi;

    public async Task<RepresentacionImpresa> ConstruirAsync(XDocument documento, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var raiz = documento.Root;
        if (raiz is null || raiz.Name != Cfdi + "Comprobante")
        {
            throw new ArgumentException("El documento no es un cfdi:Comprobante.", nameof(documento));
        }

        var conceptos = raiz.Hijo("Conceptos")?.Hijos("Concepto").ToList() ?? [];
        var clavesUnidad = conceptos
            .Select(c => c.Valor("ClaveUnidad"))
            .OfType<string>()
            .Where(clave => clave.Length is > 0 and <= LongitudMaximaDeClave)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var pequenosTarea = catalogos.PequenosAsync(cancelacion);
        var unidadesTarea = catalogos.ClavesUnidadAsync(clavesUnidad, cancelacion);
        await Task.WhenAll(pequenosTarea, unidadesTarea);
        var catalogo = pequenosTarea.Result;
        var unidades = unidadesTarea.Result;

        var emisor = raiz.Hijo("Emisor");
        var receptor = raiz.Hijo("Receptor");
        var moneda = raiz.Valor("Moneda") ?? "MXN";
        var tipo = raiz.Valor("TipoDeComprobante");
        var timbre = raiz.Hijo("Complemento")?.Element(XName.Get("TimbreFiscalDigital", EspacioDelTimbre));
        var folioFiscal = timbre?.Valor("UUID");
        var serieFolio = JuntarSerieYFolio(raiz.Valor("Serie"), raiz.Valor("Folio"));
        var nombreDelTipo = NombreDelTipo(tipo);

        var identificador = serieFolio ?? folioFiscal ?? emisor?.Valor("Rfc") ?? "sin folio";
        var rfcEmisor = emisor?.Valor("Rfc");

        return new RepresentacionImpresa
        {
            Titulo = TituloDelTipo(tipo),
            EstaTimbrado = timbre is not null,
            FolioFiscal = folioFiscal,
            SerieFolio = serieFolio,
            Version = OVacio(raiz.Valor("Version")),
            Emisor = ArmarEmisor(emisor, raiz, catalogo),
            Receptor = ArmarReceptor(receptor, catalogo),
            Datos = ArmarDatos(raiz, tipo, moneda, timbre, catalogo),
            Relacionados = ArmarRelacionados(raiz, catalogo),
            Conceptos = [.. conceptos.Select(c => ArmarConcepto(c, unidades, catalogo))],
            Totales = ArmarTotales(raiz, moneda),
            Traslados = [.. raiz.Hijo("Impuestos").Traslados().Select(t => ArmarTraslado(t, catalogo))],
            Retenciones = [.. raiz.Hijo("Impuestos").Retenciones().Select(r => ArmarRetencion(r, catalogo))],
            Sellos = ArmarSellos(documento, raiz, timbre, emisor, receptor),
            NombreDeArchivo = NombreDeArchivo(rfcEmisor, raiz.Valor("Serie"), raiz.Valor("Folio"), folioFiscal, raiz.Valor("Fecha"), timbre is not null),
            TituloDelDocumento = $"{nombreDelTipo} {identificador}",
            AsuntoDelDocumento = timbre is not null
                ? $"Representación impresa de un CFDI 4.0 de {OVacio(emisor?.Valor("Nombre"))}"
                : "Representación impresa de un CFDI 4.0 de prueba, sin timbrar y sin validez fiscal",
        };
    }

    private static ParteImpresa ArmarEmisor(XElement? emisor, XElement raiz, CatalogosPequenos catalogo)
    {
        var regimen = emisor?.Valor("RegimenFiscal");
        return new ParteImpresa(
            "Emisor",
            [
                new("Nombre", OVacio(emisor?.Valor("Nombre")), Enfasis.Nombre),
                new("RFC", OVacio(emisor?.Valor("Rfc")), Enfasis.Codigo),
                new("Régimen fiscal", Con(regimen, Descripcion(catalogo.RegimenesFiscales, regimen, r => r.Descripcion))),
                new("Lugar de expedición", OVacio(raiz.Valor("LugarExpedicion"))),
            ]);
    }

    private static ParteImpresa ArmarReceptor(XElement? receptor, CatalogosPequenos catalogo)
    {
        var uso = receptor?.Valor("UsoCFDI");
        var regimen = receptor?.Valor("RegimenFiscalReceptor");
        var datos = new List<DatoImpreso>
        {
            new("Nombre", OVacio(receptor?.Valor("Nombre")), Enfasis.Nombre),
            new("RFC", OVacio(receptor?.Valor("Rfc")), Enfasis.Codigo),
            new("Uso del CFDI", Con(uso, Descripcion(catalogo.UsosCfdi, uso, u => u.Descripcion))),
            new("Régimen fiscal", Con(regimen, Descripcion(catalogo.RegimenesFiscales, regimen, r => r.Descripcion))),
            new("Domicilio fiscal", OVacio(receptor?.Valor("DomicilioFiscalReceptor"))),
        };

        if (receptor?.Valor("ResidenciaFiscal") is { Length: > 0 } residencia)
        {
            datos.Add(new("Residencia fiscal", Con(residencia, Descripcion(catalogo.Paises, residencia, p => p.Descripcion))));
        }

        if (receptor?.Valor("NumRegIdTrib") is { Length: > 0 } registro)
        {
            datos.Add(new("Núm. de registro de identidad tributaria", registro, Enfasis.Codigo));
        }

        return new ParteImpresa("Receptor", datos);
    }

    private static List<DatoImpreso> ArmarDatos(
        XElement raiz, string? tipo, string moneda, XElement? timbre, CatalogosPequenos catalogo)
    {
        var exportacion = raiz.Valor("Exportacion");
        var formaPago = raiz.Valor("FormaPago");
        var metodoPago = raiz.Valor("MetodoPago");

        var datos = new List<DatoImpreso>
        {
            new("Fecha y hora de emisión", OVacio(raiz.Valor("Fecha")), Enfasis.Normal),
            new("Tipo de comprobante", Con(tipo, Descripcion(catalogo.TiposDeComprobante, tipo, t => t.Descripcion))),
            new("Exportación", Con(exportacion, Descripcion(catalogo.Exportaciones, exportacion, e => e.Descripcion))),
            new("Moneda", Con(moneda, Descripcion(catalogo.Monedas, moneda, m => m.Descripcion))),
            new("Tipo de cambio", OVacio(raiz.Valor("TipoCambio"))),
            new("Forma de pago", Con(formaPago, Descripcion(catalogo.FormasPago, formaPago, f => f.Descripcion))),
            new("Método de pago", Con(metodoPago, Descripcion(catalogo.MetodosPago, metodoPago, m => m.Descripcion))),
            new("Condiciones de pago", OVacio(raiz.Valor("CondicionesDePago"))),
        };

        if (raiz.Valor("Confirmacion") is { Length: > 0 } confirmacion)
        {
            datos.Add(new("Clave de confirmación", confirmacion, Enfasis.Codigo));
        }

        if (raiz.Hijo("InformacionGlobal") is { } global)
        {
            var periodicidad = global.Valor("Periodicidad");
            var meses = global.Valor("Meses");
            datos.Add(new("Periodicidad de la información global", Con(periodicidad, Descripcion(catalogo.Periodicidades, periodicidad, p => p.Descripcion))));
            datos.Add(new("Mes de la información global", Con(meses, Descripcion(catalogo.Meses, meses, m => m.Descripcion))));
            datos.Add(new("Año de la información global", OVacio(global.Valor("Año"))));
        }

        datos.Add(new("No. de serie del certificado del emisor", OVacio(raiz.Valor("NoCertificado")), Enfasis.Codigo));

        if (timbre is not null)
        {
            datos.Add(new("No. de serie del certificado del SAT", OVacio(timbre.Valor("NoCertificadoSAT")), Enfasis.Codigo));
            datos.Add(new("Fecha y hora de certificación", OVacio(timbre.Valor("FechaTimbrado"))));
            datos.Add(new("RFC del proveedor de certificación", OVacio(timbre.Valor("RfcProvCertif")), Enfasis.Codigo));
        }

        return datos;
    }

    private static List<RelacionImpresa> ArmarRelacionados(XElement raiz, CatalogosPequenos catalogo) =>
    [
        .. raiz.Hijos("CfdiRelacionados").Select(relacion =>
        {
            var tipoRelacion = relacion.Valor("TipoRelacion");
            return new RelacionImpresa(
                Con(tipoRelacion, Descripcion(catalogo.TiposRelacion, tipoRelacion, t => t.Descripcion)),
                [.. relacion.Hijos("CfdiRelacionado").Select(r => r.Valor("UUID")).OfType<string>()]);
        }),
    ];

    private static ConceptoImpreso ArmarConcepto(
        XElement concepto, IReadOnlyDictionary<string, ClaveUnidad> unidades, CatalogosPequenos catalogo)
    {
        var claveUnidad = concepto.Valor("ClaveUnidad");
        var nombreDeUnidad = concepto.Valor("Unidad") is { Length: > 0 } escrita
            ? escrita
            : claveUnidad is not null && unidades.TryGetValue(claveUnidad, out var registro) ? registro.Nombre : null;
        var unidad = nombreDeUnidad is null ? OVacio(claveUnidad) : $"{claveUnidad} · {nombreDeUnidad}";

        var impuestos = concepto.Hijo("Impuestos");
        return new ConceptoImpreso(
            ClaveProdServ: OVacio(concepto.Valor("ClaveProdServ")),
            Unidad: unidad,
            Cantidad: AgruparConDosDecimales(concepto.Valor("Cantidad")),
            NoIdentificacion: concepto.Valor("NoIdentificacion") is { Length: > 0 } numero ? numero : null,
            Descripcion: OVacio(concepto.Valor("Descripcion")),
            ValorUnitario: AgruparConDosDecimales(concepto.Valor("ValorUnitario")),
            Descuento: concepto.Valor("Descuento") is { } descuento ? AgruparConDosDecimales(descuento) : null,
            ObjetoImp: OVacio(concepto.Valor("ObjetoImp")),
            Importe: AgruparConDosDecimales(concepto.Valor("Importe")),
            Impuestos:
            [
                .. impuestos.Traslados().Select(t => LineaDeImpuesto("Traslado", t, catalogo)),
                .. impuestos.Retenciones().Select(r => LineaDeImpuesto("Retención", r, catalogo)),
            ]);
    }

    /// <summary>"Traslado IVA 16.00 %: $1,344.00", "Retención ISR 10.00 %: $840.00" o "Traslado IVA: exento".</summary>
    private static string LineaDeImpuesto(string clase, XElement impuesto, CatalogosPequenos catalogo)
    {
        var nombre = NombreDelImpuesto(impuesto.Valor("Impuesto"), catalogo);
        var factor = impuesto.Valor("TipoFactor");
        if (factor == "Exento")
        {
            return $"{clase} {nombre}: exento";
        }

        var tasa = factor == "Cuota"
            ? $"cuota ${DosDecimales(impuesto.Valor("TasaOCuota"))}"
            : $"{Porcentaje(impuesto.Valor("TasaOCuota"))} %";
        return $"{clase} {nombre} {tasa}: {Dinero(impuesto.Valor("Importe"))}";
    }

    private static TotalesImpresos ArmarTotales(XElement raiz, string moneda)
    {
        var impuestos = raiz.Hijo("Impuestos");
        return new TotalesImpresos(
            Subtotal: DineroConMoneda(raiz.Valor("SubTotal"), moneda),
            Descuento: raiz.Valor("Descuento") is { } descuento ? DineroConMoneda(descuento, moneda) : null,
            TotalTrasladados: impuestos?.Valor("TotalImpuestosTrasladados") is { } trasladados
                ? DineroConMoneda(trasladados, moneda)
                : null,
            TotalRetenidos: impuestos?.Valor("TotalImpuestosRetenidos") is { } retenidos
                ? DineroConMoneda(retenidos, moneda)
                : null,
            Total: DineroConMoneda(raiz.Valor("Total"), moneda),
            ImporteConLetra: EnLetra(raiz.Valor("Total"), moneda));
    }

    private static string? EnLetra(string? total, string moneda)
    {
        // Sin moneda (XXX, como en los comprobantes de pago y de traslado) no hay un importe que escribir con letra.
        if (moneda == "XXX"
            || !decimal.TryParse(total, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor)
            || valor < 0)
        {
            return null;
        }

        try
        {
            return ImporteConLetra.De(valor, moneda);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static ImpuestoImpreso ArmarTraslado(XElement traslado, CatalogosPequenos catalogo) =>
        new(
            Base: traslado.Valor("Base") is { } @base ? AgruparConDosDecimales(@base) : null,
            Impuesto: ClaveYNombreDelImpuesto(traslado.Valor("Impuesto"), catalogo),
            Factor: traslado.Valor("TipoFactor"),
            TasaOCuota: traslado.Valor("TasaOCuota"),
            Importe: traslado.Valor("Importe") is { } importe ? Dinero(importe) : null);

    private static ImpuestoImpreso ArmarRetencion(XElement retencion, CatalogosPequenos catalogo) =>
        new(
            Base: null,
            Impuesto: ClaveYNombreDelImpuesto(retencion.Valor("Impuesto"), catalogo),
            Factor: null,
            TasaOCuota: null,
            Importe: retencion.Valor("Importe") is { } importe ? Dinero(importe) : null);

    private static SellosImpresos ArmarSellos(
        XDocument documento, XElement raiz, XElement? timbre, XElement? emisor, XElement? receptor)
    {
        var selloDelComprobante = SinEspacios(raiz.Valor("Sello"));
        if (timbre is null)
        {
            return new SellosImpresos(
                UrlDeVerificacion: null,
                TituloDeLaCadena: "Cadena original del comprobante",
                Cadena: CadenaOriginal.Generar(documento),
                SelloDelEmisor: selloDelComprobante.Length > 0 ? selloDelComprobante : null,
                SelloDelSat: null);
        }

        // El del timbre es el mismo sello del emisor; si el PAC lo omitió, sirve el del comprobante.
        var selloDelEmisor = SinEspacios(timbre.Valor("SelloCFD")) is { Length: > 0 } copia ? copia : selloDelComprobante;
        return new SellosImpresos(
            UrlDeVerificacion: UrlDeVerificacion(
                timbre.Valor("UUID"), emisor?.Valor("Rfc"), receptor?.Valor("Rfc"), raiz.Valor("Total"), selloDelEmisor),
            TituloDeLaCadena: "Cadena original del complemento de certificación digital del SAT",
            Cadena: CadenaOriginal.DelTimbre(timbre),
            SelloDelEmisor: selloDelEmisor.Length > 0 ? selloDelEmisor : null,
            SelloDelSat: SinEspacios(timbre.Valor("SelloSAT")) is { Length: > 0 } selloSat ? selloSat : null);
    }

    /// <summary>
    /// La dirección que lleva el código QR (Anexo 20, rubro I.E): UUID, RFC del emisor, RFC del receptor, total con diez
    /// enteros y seis decimales, y los últimos ocho caracteres del sello del emisor.
    /// </summary>
    internal static string UrlDeVerificacion(string? uuid, string? rfcEmisor, string? rfcReceptor, string? total, string selloDelEmisor)
    {
        var importe = decimal.TryParse(total, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : 0m;
        var ultimosOcho = selloDelEmisor.Length > 8 ? selloDelEmisor[^8..] : selloDelEmisor;

        // El RFC puede llevar "&" (A&A010101AA1) y el sello "+", "/" o "=": sin escapar, el SAT leería otra cosa.
        return DireccionDeVerificacion
            + "?id=" + Uri.EscapeDataString(uuid ?? string.Empty)
            + "&re=" + Uri.EscapeDataString(rfcEmisor ?? string.Empty)
            + "&rr=" + Uri.EscapeDataString(rfcReceptor ?? string.Empty)
            + "&tt=" + importe.ToString("0000000000.000000", CultureInfo.InvariantCulture)
            + "&fe=" + Uri.EscapeDataString(ultimosOcho);
    }

    /// <summary>"RFC-Serie-Folio-prueba.pdf"; sin serie ni folio, el UUID o la fecha; sin "-prueba" si ya está timbrado.</summary>
    internal static string NombreDeArchivo(string? rfc, string? serie, string? folio, string? uuid, string? fecha, bool timbrado)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(rfc))
        {
            partes.Add(rfc.Trim());
        }

        if (!string.IsNullOrWhiteSpace(serie))
        {
            partes.Add(serie.Trim());
        }

        if (!string.IsNullOrWhiteSpace(folio))
        {
            partes.Add(folio.Trim());
        }

        if (string.IsNullOrWhiteSpace(serie) && string.IsNullOrWhiteSpace(folio))
        {
            var fechaSinSignos = new string([.. (fecha ?? string.Empty).Where(char.IsAsciiDigit)]);
            partes.Add(!string.IsNullOrWhiteSpace(uuid) ? uuid.Trim() : fechaSinSignos.Length > 0 ? fechaSinSignos : "sin-folio");
        }

        // Sin acentos ni virgulillas (IAÑL es IANL) y sin lo que un sistema de archivos no admite (H&E es H_E).
        var sinMarcas = new string(
            [.. string.Join('-', partes).Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
        var nombre = CaracteresNoPermitidos().Replace(sinMarcas, "_");
        return timbrado ? $"{nombre}.pdf" : $"{nombre}-prueba.pdf";
    }

    private static string? JuntarSerieYFolio(string? serie, string? folio)
    {
        var partes = new[] { serie, folio }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).ToList();
        return partes.Count == 0 ? null : string.Join('-', partes);
    }

    private static string TituloDelTipo(string? tipo) => tipo switch
    {
        "I" => "FACTURA",
        "E" => "NOTA DE CRÉDITO",
        "T" => "TRASLADO",
        "N" => "NÓMINA",
        "P" => "PAGO",
        _ => "COMPROBANTE",
    };

    private static string NombreDelTipo(string? tipo) => tipo switch
    {
        "I" => "Factura",
        "E" => "Nota de crédito",
        "T" => "Comprobante de traslado",
        "N" => "Recibo de nómina",
        "P" => "Comprobante de pago",
        _ => "Comprobante",
    };

    private static string NombreDelImpuesto(string? clave, CatalogosPequenos catalogo) =>
        Descripcion(catalogo.Impuestos, clave, i => i.Descripcion) ?? OVacio(clave);

    private static string ClaveYNombreDelImpuesto(string? clave, CatalogosPequenos catalogo) =>
        Descripcion(catalogo.Impuestos, clave, i => i.Descripcion) is { } nombre ? $"{clave} {nombre}" : OVacio(clave);

    /// <summary>La descripción de una clave en su catálogo, o nulo si la clave no está.</summary>
    private static string? Descripcion<T>(IReadOnlyDictionary<string, T> catalogo, string? clave, Func<T, string> descripcion) =>
        clave is not null && catalogo.TryGetValue(clave, out var registro) ? descripcion(registro) : null;

    [GeneratedRegex("[^A-Za-z0-9._-]+")]
    private static partial Regex CaracteresNoPermitidos();
}
