using System.Xml.Linq;
using Comprobante.Core.Validacion.Reglas;
using static Comprobante.UnitTests.Validacion.Siembra;

namespace Comprobante.UnitTests.Validacion;

/// <summary>
/// Un error sembrado para una regla de la matriz del SAT: el escenario de partida, cómo se rompe y qué debe salir.
/// </summary>
/// <param name="Id">El código de la regla; si hay varios casos de la misma, lleva <c>#2</c>, <c>#3</c>...</param>
/// <param name="Atributo">El atributo que el hallazgo debe señalar, si la regla señala uno.</param>
/// <param name="Ademas">Los códigos que salen junto con el esperado porque el error sembrado también rompe otra cuenta (el total, el resumen de impuestos...).</param>
internal sealed record CasoDeRegla(
    string Id,
    string Codigo,
    string Escenario,
    Action<XDocument> Sembrar,
    string? Atributo,
    IReadOnlyList<string> Ademas);

/// <summary>
/// El banco de errores sembrados: al menos un caso por cada regla de la matriz que la app revisa (total o parcialmente). Una
/// prueba comprueba que ninguna regla se quede sin caso.
/// </summary>
internal static class CasosDeReglas
{
    private const string Basico = "ingreso-basico";
    private const string Retenciones = "ingreso-retenciones";
    private const string Dolares = "ingreso-dolares";
    private const string Publico = "publico-en-general";
    private const string Egreso = "egreso-relacionado";

    public static IReadOnlyList<CasoDeRegla> Todos { get; } = Crear();

    public static CasoDeRegla Buscar(string id) => Todos.Single(c => c.Id == id);

    private static List<CasoDeRegla> Crear()
    {
        var casos = new List<CasoDeRegla>();

        void Caso(string codigo, string escenario, Action<XDocument> sembrar, string? atributo = null, params string[] ademas)
        {
            var anteriores = casos.Count(c => c.Codigo == codigo);
            casos.Add(new CasoDeRegla(anteriores == 0 ? codigo : $"{codigo}#{anteriores + 1}", codigo, escenario, sembrar, atributo, ademas));
        }

        // ---- El nodo Comprobante ----

        Caso("CFDI40101", Basico, d => d.Poner("", "Fecha", "2026-13-45T10:00:00"), "Fecha");
        Caso("CFDI40101", Basico, d => d.Poner("", "Fecha", "2026-02-30T10:00:00"), "Fecha");
        Caso("CFDI40103", Basico, d => { ComoTraslado(d); d.Poner("", "FormaPago", "01"); }, "FormaPago");
        Caso("CFDI40104", Basico, d => d.Poner("", "FormaPago", "77"), "FormaPago");
        Caso("CFDI40105", Basico, d => d.Poner("", "MetodoPago", "PPD"), "FormaPago");
        Caso("CFDI40107", Basico, d => d.Poner("", "SubTotal", "1000.000"), "SubTotal");
        Caso("CFDI40108", Basico, d => d.Poner("", "SubTotal", "1001.00"), "SubTotal", "CFDI40119");
        Caso("CFDI40109", Basico, d => { ComoTraslado(d); d.Poner("", "SubTotal", "10"); }, "SubTotal", "CFDI40119");
        Caso("CFDI40110", Retenciones, d => d.Poner("", "Descuento", "20000.00"), "Descuento", "CFDI40111", "CFDI40119");
        Caso("CFDI40111", Retenciones, d => d.Poner("", "Descuento", "50.00"), "Descuento", "CFDI40119");
        Caso("CFDI40111", Retenciones, d => d.Poner("", "Descuento", null), "Descuento", "CFDI40119");
        Caso("CFDI40111", Basico, d => d.Poner("", "Descuento", "10.00"), "Descuento", "CFDI40119");
        Caso("CFDI40112", Retenciones, d => d.Poner("", "Descuento", "100.000"), "Descuento");
        Caso("CFDI40113", Basico, d => d.Poner("", "Moneda", "ZZZ"), "Moneda", "CFDI40115");
        Caso("CFDI40114", Basico, d => d.Poner("", "TipoCambio", "2"), "TipoCambio");
        Caso("CFDI40115", Dolares, d => d.Poner("", "TipoCambio", null), "TipoCambio");
        Caso("CFDI40116", Basico, d => { ComoTraslado(d); d.Poner("", "TipoCambio", "1"); }, "TipoCambio");
        Caso("CFDI40117", Dolares, d => d.Poner("", "TipoCambio", "17.4521234"), "TipoCambio");
        Caso("CFDI40119", Basico, d => d.Poner("", "Total", "1161.00"), "Total");
        Caso("CFDI40121", Basico, d => d.Poner("", "TipoDeComprobante", "Z"), "TipoDeComprobante");
        Caso("CFDI40122", Basico, d => d.Poner("", "Exportacion", "02"), "Exportacion");
        Caso("CFDI40123", Basico, d => d.Poner("", "Exportacion", "09"), "Exportacion");
        Caso("CFDI40124", Basico, d => d.Poner("", "MetodoPago", "XYZ"), "MetodoPago");
        Caso("CFDI40125", Basico, d => { ComoTraslado(d); d.Poner("", "MetodoPago", "PUE"); }, "MetodoPago");
        Caso("CFDI40126", Basico, d => d.Poner("", "LugarExpedicion", "00000"), "LugarExpedicion");
        Caso("CFDI40222", Basico, d => ConComercioExterior(d, exportacionDefinitiva: false), "Exportacion", "CMP004");

        // ---- InformacionGlobal y CfdiRelacionados ----

        Caso("CFDI40130", Publico, d => d.Quitar("InformacionGlobal"));
        Caso("CFDI40131", Publico, d => d.Poner("InformacionGlobal", "Periodicidad", "09"), "Periodicidad");
        Caso("CFDI40132", Publico, d => { d.Poner("InformacionGlobal", "Periodicidad", "05"); d.Poner("InformacionGlobal", "Meses", "13"); }, "Periodicidad");
        Caso("CFDI40133", Publico, d => d.Poner("InformacionGlobal", "Meses", "20"), "Meses");
        Caso("CFDI40134", Publico, d => d.Poner("InformacionGlobal", "Meses", "13"), "Meses");
        Caso("CFDI40135", Publico, d => { d.Poner("Emisor", "RegimenFiscal", "621"); d.Poner("InformacionGlobal", "Periodicidad", "05"); }, "Meses");
        Caso("CFDI40136", Publico, d => d.Poner("InformacionGlobal", "Año", "2019"), "Año");
        Caso("CFDI40137", Egreso, d => d.Poner("CfdiRelacionados", "TipoRelacion", "99"), "TipoRelacion");

        // ---- Emisor y receptor ----

        Caso("CFDI40140", Basico, d => d.Poner("Emisor", "RegimenFiscal", "999"), "RegimenFiscal");
        Caso("CFDI40141", Basico, d => d.Poner("Emisor", "RegimenFiscal", "605"), "RegimenFiscal");
        Caso("CFDI40146", Basico, d => d.Poner("Receptor", "Nombre", "PUBLICO EN GENERAL"), "Rfc");
        Caso("CFDI40149", Publico, d => d.Poner("Receptor", "DomicilioFiscalReceptor", "06002"), "DomicilioFiscalReceptor");
        Caso("CFDI40150", Publico, d => { ComoExtranjero(d); d.Poner("Receptor", "ResidenciaFiscal", "QQQ"); }, "ResidenciaFiscal");
        Caso("CFDI40151", Basico, d => d.Poner("Receptor", "ResidenciaFiscal", "USA"), "ResidenciaFiscal");
        Caso("CFDI40152", Publico, d => { ComoExtranjero(d); d.Poner("Receptor", "ResidenciaFiscal", "MEX"); }, "ResidenciaFiscal");
        Caso("CFDI40153", Publico, d => { ComoExtranjero(d); d.Poner("Receptor", "ResidenciaFiscal", null); d.Poner("Receptor", "NumRegIdTrib", "123456789"); });
        Caso("CFDI40154", Basico, d => d.Poner("Receptor", "NumRegIdTrib", "123456789"), "NumRegIdTrib");
        Caso("CFDI40155", Publico, d => { ComoExtranjero(d); ConComercioExterior(d); }, null, "CMP004");
        Caso("CFDI40156", Publico, d => { ComoExtranjero(d); d.Poner("Receptor", "NumRegIdTrib", "ABC123"); }, "NumRegIdTrib");
        Caso("CFDI40157", Basico, d => d.Poner("Receptor", "RegimenFiscalReceptor", "999"), "RegimenFiscalReceptor");
        Caso("CFDI40158", Basico, d => d.Poner("Receptor", "RegimenFiscalReceptor", "612"), "RegimenFiscalReceptor");
        Caso("CFDI40159", Publico, d => d.Poner("Receptor", "RegimenFiscalReceptor", "612"), "RegimenFiscalReceptor");
        Caso("CFDI40160", Basico, d => d.Poner("Receptor", "UsoCFDI", "Z99"), "UsoCFDI");
        Caso("CFDI40161", Basico, d => d.Poner("Receptor", "UsoCFDI", "D01"), "UsoCFDI");
        Caso("CFDI40161", Egreso, d => d.Poner("Receptor", "UsoCFDI", "CN01"), "UsoCFDI");

        // ---- Conceptos: claves, importes y objeto de impuesto ----

        Caso("CFDI40162", Basico, d => d.Poner(Concepto, "ClaveProdServ", "99999999"), "ClaveProdServ");
        Caso("CFDI40162", Basico, d => { d.Poner("", "Fecha", "2022-06-01T10:00:00"); d.Poner(Concepto, "ClaveProdServ", "78111505"); }, "ClaveProdServ");
        Caso("CFDI40164", Basico, d => { IepsEnLugarDeIva(d); d.Poner(Concepto, "ClaveProdServ", "80111700"); }, "ClaveProdServ");
        Caso("CFDI140228", Basico, d => d.Poner(Concepto, "ClaveProdServ", "15101505"), "ClaveProdServ");
        Caso("CFDI40165", Basico, d => d.Poner(Concepto, "ClaveUnidad", "ZZZ"), "ClaveUnidad");
        Caso("CFDI40166", Basico, d =>
        {
            SinImpuestos(d);
            d.Poner(Concepto, "ValorUnitario", "0.00");
            d.Poner(Concepto, "Importe", "0.00");
            d.Poner("", "SubTotal", "0.00");
            d.Poner("", "Total", "0.00");
        }, "ValorUnitario");
        // Con Cantidad 1 (sin decimales) el SAT admite un Importe de entre 0.5 y 1.5 veces el valor unitario: 999.00 todavía cabe.
        Caso("CFDI40167", Basico, d => d.Poner(Concepto, "Importe", "2000.00"), "Importe", "CFDI40108");
        Caso("CFDI40168", Retenciones, d => d.Poner("Conceptos/Concepto[1]", "Descuento", "100.000"), "Descuento");
        Caso("CFDI40169", Retenciones, d => d.Poner("Conceptos/Concepto[1]", "Descuento", "9000.00"), "Descuento", "CFDI40111");
        Caso("CFDI40170", Basico, d => d.Poner(Concepto, "ObjetoImp", "99"), "ObjetoImp");
        Caso("CFDI40171", Basico, d => { SinImpuestos(d); d.Poner(Concepto, "ObjetoImp", "02"); }, "ObjetoImp");
        Caso("CFDI40172", Basico, d => d.Poner(Concepto, "ObjetoImp", "01"));
        Caso("CFDI40173", Basico, d =>
        {
            SinImpuestos(d);
            d.Poner(Concepto, "ObjetoImp", "02");
            d.Nodo(Concepto).Add(new XElement(Cfdi + "Impuestos"));
        });
        Caso("CFDI140226", Basico, d => d.Poner(Concepto, "ObjetoImp", "06"), "Impuesto");
        Caso("CFDI140227", Basico, d => d.Poner(Concepto, "ObjetoImp", "07"), "Impuesto");

        // ---- Impuestos de cada concepto ----

        Caso("CFDI40174", Basico, d => d.Poner(TrasladoDeConcepto, "Base", "0"), "Base", "CFDI40180", "CFDI40215");
        Caso("CFDI40175", Basico, d => d.Poner(TrasladoDeConcepto, "Impuesto", "009"), "Impuesto", "CFDI40215", "CFDI40221");
        Caso("CFDI40176", Basico, d => d.Poner(TrasladoDeConcepto, "TipoFactor", "Otro"), "TipoFactor", "CFDI40215", "CFDI40221");
        Caso("CFDI40177", Retenciones, d => d.Poner("Conceptos/Concepto[3]/Impuestos/Traslados/Traslado", "TasaOCuota", "0.160000"), "TasaOCuota");
        Caso("CFDI40178", Basico, d => d.Poner(TrasladoDeConcepto, "Importe", null), "Importe", "CFDI40221");
        Caso("CFDI40179", Basico, TasaInvalida, "TasaOCuota", "CFDI40219");
        Caso("CFDI40180", Basico, d => d.Poner(TrasladoDeConcepto, "Importe", "150.00"), "Importe", "CFDI40221");
        Caso("CFDI40181", Retenciones, d => d.Poner(RetencionDeConcepto + "[1]", "Base", "0"), "Base", "CFDI40186");
        Caso("CFDI40182", Retenciones, d => d.Poner(RetencionDeConcepto + "[1]", "Impuesto", "009"), "Impuesto", "CFDI40211");
        Caso("CFDI40183", Retenciones, d => d.Poner(RetencionDeConcepto + "[1]", "TipoFactor", "Otro"), "TipoFactor");
        Caso("CFDI40184", Retenciones, d => d.Poner(RetencionDeConcepto + "[1]", "TipoFactor", "Exento"), "TipoFactor");
        Caso("CFDI40185", Retenciones, d =>
        {
            d.Poner(RetencionDeConcepto + "[1]", "TasaOCuota", "0.400000");
            d.Poner(RetencionDeConcepto + "[1]", "Importe", "3360.00");
        }, "TasaOCuota", "CFDI40211");
        Caso("CFDI40186", Retenciones, d => d.Poner(RetencionDeConcepto + "[1]", "Importe", "800.00"), "Importe", "CFDI40211");

        // ---- A cuenta de terceros, información aduanera y partes ----

        Caso("CFDI40188", Basico, d => ConTerceros(d, "EKU9003173C9", "ESCUELA KEMPER URGATE", "601", "42501"), "RfcACuentaTerceros");
        Caso("CFDI40189", Basico, d => ConTerceros(d, ReglasConceptos.RfcExtranjeroTerceros, "OTRO NOMBRE", "616", "42501"), "NombreACuentaTerceros");
        Caso("CFDI40191", Basico, d => ConTerceros(d, "AAA010101AAA", "TERCERO SA", "999", "42501"), "RegimenFiscalACuentaTerceros");
        Caso("CFDI140223", Basico, d => ConTerceros(d, ReglasConceptos.RfcExtranjeroTerceros, ReglasConceptos.NombreExtranjeroTerceros, "601", "42501"), "RegimenFiscalACuentaTerceros");
        Caso("CFDI40193", Basico, d => ConTerceros(d, ReglasConceptos.RfcExtranjeroTerceros, ReglasConceptos.NombreExtranjeroTerceros, "616", "01010"), "DomicilioFiscalACuentaTerceros");
        Caso("CFDI40195", Basico, d =>
        {
            ConComercioExterior(d);
            d.Nodo("Conceptos/Concepto/Impuestos").AddAfterSelf(Aduanera());
        }, "NumeroPedimento", "CMP004");
        Caso("CFDI40196", Basico, d => ConParte(d, "99999999", "1", null, null), "ClaveProdServ");
        Caso("CFDI40197", Basico, d => ConParte(d, "84111506", "1", "0.00", "0.00"), "ValorUnitario");
        Caso("CFDI40198", Basico, d => ConParte(d, "84111506", "2", "10.00", "50.00"), "Importe");
        Caso("CFDI40200", Basico, d =>
        {
            ConComercioExterior(d);
            ConParte(d, "84111506", "1", null, null, conAduanera: true);
        }, "NumeroPedimento", "CMP004");

        // ---- El resumen de impuestos ----

        Caso("CFDI40201", Basico, d =>
        {
            ComoTraslado(d);
            d.Nodo("Conceptos").AddAfterSelf(new XElement(Cfdi + "Impuestos"));
        });
        Caso("CFDI40202", Retenciones, d => d.Poner("Impuestos", "TotalImpuestosRetenidos", "1736.000"), "TotalImpuestosRetenidos");
        Caso("CFDI40203", Retenciones, d => d.Poner("Impuestos", "TotalImpuestosRetenidos", "1700.00"), "TotalImpuestosRetenidos", "CFDI40119");
        Caso("CFDI40204", Basico, d => d.Poner("Impuestos", "TotalImpuestosTrasladados", "160.000"), "TotalImpuestosTrasladados");
        Caso("CFDI40205", Basico, d => d.Poner("Impuestos", "TotalImpuestosTrasladados", "161.00"), "TotalImpuestosTrasladados", "CFDI40119");
        Caso("CFDI40206", Retenciones, d => d.Poner("Impuestos", "TotalImpuestosRetenidos", null), "TotalImpuestosRetenidos", "CFDI40119");
        Caso("CFDI40207", Retenciones, d => d.Poner("Impuestos/Retenciones/Retencion[1]", "Impuesto", "009"), "Impuesto", "CFDI40211");
        Caso("CFDI40208", Retenciones, d => d.Nodo("Impuestos/Retenciones").Add(new XElement(
            Cfdi + "Retencion", new XAttribute("Impuesto", "001"), new XAttribute("Importe", "840.00"))), "Impuesto", "CFDI40203");
        Caso("CFDI40210", Retenciones, d => d.Poner("Impuestos/Retenciones/Retencion[1]", "Importe", "840.000"), "Importe");
        Caso("CFDI40211", Retenciones, d => d.Poner("Impuestos/Retenciones/Retencion[1]", "Importe", "850.00"), "Importe", "CFDI40203");
        Caso("CFDI40212", Basico, d => d.Poner("Impuestos", "TotalImpuestosTrasladados", null), "TotalImpuestosTrasladados", "CFDI40119");
        Caso("CFDI40213", Retenciones, d => d.Poner("Impuestos/Traslados/Traslado[3]", "Importe", "0.00"), "Importe");
        Caso("CFDI40214", Basico, d => d.Poner(TrasladoDelResumen, "Base", "1000.000"), "Base");
        Caso("CFDI40215", Basico, d => d.Poner(TrasladoDelResumen, "Base", "999.00"), "Base");
        Caso("CFDI40216", Retenciones, d => d.Poner("Impuestos/Traslados/Traslado[3]", "Base", "199.00"), "Base");
        Caso("CFDI40217", Basico, d => d.Poner(TrasladoDelResumen, "Impuesto", "009"), "Impuesto", "CFDI40215", "CFDI40221");
        Caso("CFDI40218", Basico, d => d.Nodo("Impuestos/Traslados").Add(new XElement(d.Nodo(TrasladoDelResumen))), null, "CFDI40205");
        Caso("CFDI40219", Basico, TasaInvalida, "TasaOCuota", "CFDI40179");
        Caso("CFDI40220", Basico, d => d.Poner(TrasladoDelResumen, "Importe", "160.000"), "Importe");
        Caso("CFDI40221", Basico, d => d.Poner(TrasladoDelResumen, "Importe", "161.00"), "Importe", "CFDI40205");

        return casos;
    }
}
