using System.Text.RegularExpressions;
using System.Xml.Linq;
using Comprobante.Core.Calculo;
using Comprobante.Core.Generacion;

namespace Comprobante.Core.Validacion.Reglas;

/// <summary>
/// Las reglas del nodo Comprobante de la matriz de errores del SAT: fecha, forma y método de pago, moneda y tipo de cambio,
/// subtotal, descuento y total, tipo de comprobante, exportación, lugar de expedición, y los nodos InformacionGlobal y
/// CfdiRelacionados.
/// </summary>
internal static partial class ReglasComprobante
{
    private static readonly XNamespace ImpuestosLocales = "http://www.sat.gob.mx/implocal";

    [GeneratedRegex(@"^20[1-9][0-9]-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T(([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9])\z")]
    private static partial Regex PatronFecha();

    [GeneratedRegex(@"^[0-9]{1,18}(\.[0-9]{1,6})?\z")]
    private static partial Regex PatronTipoCambio();

    public static void Revisar(ContextoValidacion c)
    {
        Fecha(c);
        Pago(c);
        Moneda(c);
        Importes(c);
        Total(c);
        TipoYExportacion(c);
        c.EnCatalogo(c.CodigosPostales, "CFDI40126", c.Raiz, "LugarExpedicion", "c_CodigoPostal");
        InformacionGlobal(c);
        Relacionados(c);
    }

    /// <summary>CFDI40101: el formato de la fecha y que sea una fecha real; además, un aviso si todavía no llega en el lugar de expedición.</summary>
    private static void Fecha(ContextoValidacion c)
    {
        var texto = c.Raiz.Valor("Fecha");
        if (texto is null)
        {
            return;
        }

        if (!PatronFecha().IsMatch(texto) || c.Fecha is null)
        {
            c.Error(
                "CFDI40101",
                c.Raiz,
                "Fecha",
                "El campo Fecha no cumple con el patrón requerido: debe ser una fecha y hora reales con el formato AAAA-MM-DDThh:mm:ss, sin zona horaria.",
                texto,
                "AAAA-MM-DDThh:mm:ss");
            return;
        }

        if (c.CodigoPostalDeExpedicion is { } lugar)
        {
            // Un minuto de margen: los relojes de quien emite y de quien revisa no están sincronizados al segundo.
            var local = ZonaHoraria.Local(c.Ahora, lugar.HusoHorario);
            if (c.Fecha > local.AddMinutes(1))
            {
                c.Advertencia(
                    Codigos.FechaEnElFuturo,
                    c.Raiz,
                    "Fecha",
                    $"La Fecha es posterior a la hora actual en el lugar de expedición ({ZonaHoraria.ComoFecha(local)}, {lugar.HusoHorario}): un PAC no timbraría el comprobante hasta que llegue esa hora.",
                    texto,
                    ZonaHoraria.ComoFecha(local));
            }
        }
    }

    /// <summary>CFDI40103, CFDI40104, CFDI40105, CFDI40124 y CFDI40125: forma y método de pago.</summary>
    private static void Pago(ContextoValidacion c)
    {
        var raiz = c.Raiz;
        var formaPago = raiz.Valor("FormaPago");
        var sinPago = c.Tipo is "T" or "N" or "P";

        if (formaPago is not null && sinPago)
        {
            c.Error("CFDI40103", raiz, "FormaPago", $"Con el tipo de comprobante {c.Tipo} no se registra FormaPago: quita el atributo.", formaPago);
        }

        c.EnCatalogo(c.Catalogos.FormasPago, "CFDI40104", raiz, "FormaPago", "c_FormaPago");

        if (raiz.Valor("MetodoPago") == "PPD" && formaPago != "99")
        {
            c.Error(
                "CFDI40105",
                raiz,
                "FormaPago",
                "Con el método de pago PPD (pago en parcialidades o diferido) la FormaPago debe ser 99 (por definir).",
                formaPago,
                "99");
        }

        if (raiz.Valor("MetodoPago") is { } metodo)
        {
            c.EnCatalogo(c.Catalogos.MetodosPago, "CFDI40124", raiz, "MetodoPago", "c_MetodoPago");
            if (c.Tipo is "T" or "P")
            {
                c.Error("CFDI40125", raiz, "MetodoPago", $"Con el tipo de comprobante {c.Tipo} se omite MetodoPago: quita el atributo.", metodo);
            }
        }
    }

    /// <summary>CFDI40113 a CFDI40117: la moneda y su tipo de cambio.</summary>
    private static void Moneda(ContextoValidacion c)
    {
        var raiz = c.Raiz;
        var moneda = raiz.Valor("Moneda");
        var tipoCambio = raiz.Valor("TipoCambio");

        c.EnCatalogo(c.Catalogos.Monedas, "CFDI40113", raiz, "Moneda", "c_Moneda");

        if (tipoCambio is not null && !PatronTipoCambio().IsMatch(tipoCambio))
        {
            c.Error(
                "CFDI40117",
                raiz,
                "TipoCambio",
                "El campo TipoCambio no cumple con el patrón requerido: hasta 18 enteros y hasta 6 decimales, solo dígitos y punto.",
                tipoCambio);
        }

        switch (moneda)
        {
            case "MXN":
                if (ExtensionesXml.Numero(tipoCambio) is { } valor && valor != 1m)
                {
                    c.Error("CFDI40114", raiz, "TipoCambio", "Con la moneda MXN el TipoCambio se omite o vale 1.", tipoCambio, "1");
                }

                break;
            case "XXX":
                if (tipoCambio is not null)
                {
                    c.Error("CFDI40116", raiz, "TipoCambio", "Con la moneda XXX (sin moneda) no se registra TipoCambio: quita el atributo.", tipoCambio);
                }

                break;
            case not null:
                if (tipoCambio is null)
                {
                    c.Error("CFDI40115", raiz, "TipoCambio", $"Con la moneda {moneda} el TipoCambio es obligatorio: es el valor de la moneda en pesos mexicanos.");
                }

                break;
        }
    }

    /// <summary>CFDI40107 a CFDI40112: SubTotal y Descuento frente a la moneda y frente a los conceptos.</summary>
    private static void Importes(ContextoValidacion c)
    {
        var raiz = c.Raiz;
        var decimales = c.DecimalesMoneda;
        var subTotal = raiz.Numero("SubTotal");
        var descuento = raiz.Numero("Descuento");

        c.DecimalesDeLaMoneda("CFDI40107", raiz, "SubTotal");
        c.DecimalesDeLaMoneda("CFDI40112", raiz, "Descuento");

        // SubTotal contra la suma de los importes de los conceptos.
        if (c.EsIngresoEgresoONomina && subTotal is { } subTotalLeido && decimales is { } d)
        {
            var importes = c.Conceptos.Select(x => x.Numero("Importe")).ToList();
            if (importes.All(i => i is not null))
            {
                var esperado = Redondeo.Redondear(importes.Sum(i => i!.Value), d);
                if (subTotalLeido != esperado)
                {
                    c.Error(
                        "CFDI40108",
                        raiz,
                        "SubTotal",
                        $"El SubTotal ({raiz.Valor("SubTotal")}) debe ser igual al redondeo de la suma de los importes de los conceptos ({Formato.Fijo(esperado, d)}).",
                        raiz.Valor("SubTotal"),
                        Formato.Fijo(esperado, d));
                }
            }
        }

        if (c.Tipo is "T" or "P" && subTotal is { } distinto && distinto != 0m)
        {
            c.Error("CFDI40109", raiz, "SubTotal", $"Con el tipo de comprobante {c.Tipo} el SubTotal debe ser 0.", raiz.Valor("SubTotal"), "0");
        }

        if (descuento is { } descuentoLeido && subTotal is { } tope && descuentoLeido > tope)
        {
            c.Error("CFDI40110", raiz, "Descuento", $"El Descuento ({raiz.Valor("Descuento")}) no puede ser mayor que el SubTotal ({raiz.Valor("SubTotal")}).", raiz.Valor("Descuento"));
        }

        // Descuento contra la suma de los descuentos de los conceptos.
        var conDescuento = c.Conceptos.Where(x => x.Tiene("Descuento")).ToList();
        if (c.EsIngresoEgresoONomina && conDescuento.Count > 0)
        {
            if (!raiz.Tiene("Descuento"))
            {
                c.Error("CFDI40111", raiz, "Descuento", "Hay conceptos con Descuento: el comprobante debe traer Descuento con la suma de los descuentos de los conceptos.");
            }
            else if (descuento is { } presente && decimales is { } d2 && conDescuento.All(x => x.Numero("Descuento") is not null))
            {
                var esperado = Redondeo.Redondear(conDescuento.Sum(x => x.Numero("Descuento")!.Value), d2);
                if (presente != esperado)
                {
                    c.Error(
                        "CFDI40111",
                        raiz,
                        "Descuento",
                        $"El Descuento ({raiz.Valor("Descuento")}) debe ser igual al redondeo de la suma de los descuentos de los conceptos ({Formato.Fijo(esperado, d2)}).",
                        raiz.Valor("Descuento"),
                        Formato.Fijo(esperado, d2));
                }
            }
        }
        else if (raiz.Tiene("Descuento"))
        {
            c.Error(
                "CFDI40111",
                raiz,
                "Descuento",
                "Solo se registra Descuento cuando el tipo de comprobante es I, E o N y algún concepto lleva Descuento: quita el atributo.",
                raiz.Valor("Descuento"));
        }
    }

    /// <summary>CFDI40119: el Total es el subtotal menos el descuento, más los impuestos trasladados y menos los retenidos.</summary>
    private static void Total(ContextoValidacion c)
    {
        var raiz = c.Raiz;
        if (raiz.Numero("Total") is not { } total
            || raiz.Numero("SubTotal") is not { } subTotal
            || c.DecimalesMoneda is not { } decimales)
        {
            return;
        }

        var impuestos = raiz.Hijo("Impuestos");
        var locales = raiz.Hijo("Complemento")?.Element(ImpuestosLocales + "ImpuestosLocales");
        var descuento = MontoOpcional(raiz, "Descuento");
        var trasladados = MontoOpcional(impuestos, "TotalImpuestosTrasladados");
        var retenidos = MontoOpcional(impuestos, "TotalImpuestosRetenidos");
        var localesTrasladados = MontoOpcional(locales, "TotaldeTraslados");
        var localesRetenidos = MontoOpcional(locales, "TotaldeRetenciones");
        if (descuento is null || trasladados is null || retenidos is null || localesTrasladados is null || localesRetenidos is null)
        {
            return;
        }

        var esperado = Redondeo.Redondear(
            subTotal - descuento.Value + trasladados.Value + localesTrasladados.Value - retenidos.Value - localesRetenidos.Value,
            decimales);
        if (total != esperado)
        {
            c.Error(
                "CFDI40119",
                raiz,
                "Total",
                $"El Total ({raiz.Valor("Total")}) no corresponde con el SubTotal menos el Descuento, más los impuestos trasladados y menos los retenidos: debería ser {Formato.Fijo(esperado, decimales)}.",
                raiz.Valor("Total"),
                Formato.Fijo(esperado, decimales));
        }
    }

    /// <summary>El valor de un atributo opcional como número: cero si no está y nulo si está pero no es un número.</summary>
    private static decimal? MontoOpcional(XElement? nodo, string atributo) =>
        nodo is null || !nodo.Tiene(atributo) ? 0m : nodo.Numero(atributo);

    /// <summary>CFDI40121, CFDI40122, CFDI40123 y CFDI40222: tipo de comprobante y exportación.</summary>
    private static void TipoYExportacion(ContextoValidacion c)
    {
        var raiz = c.Raiz;
        c.EnCatalogo(c.Catalogos.TiposDeComprobante, "CFDI40121", raiz, "TipoDeComprobante", "c_TipoDeComprobante");
        c.EnCatalogo(c.Catalogos.Exportaciones, "CFDI40123", raiz, "Exportacion", "c_Exportacion");

        var exportacion = raiz.Valor("Exportacion");
        if (exportacion == "02" && !c.TieneComercioExterior)
        {
            c.Error("CFDI40122", raiz, "Exportacion", "Con Exportacion 02 (definitiva con clave A1) el comprobante debe incluir el complemento de Comercio Exterior.", exportacion);
        }

        if (exportacion is "01" or "03" && c.TieneComercioExterior)
        {
            c.Error("CFDI40222", raiz, "Exportacion", $"Con Exportacion {exportacion} el comprobante no debe incluir el complemento de Comercio Exterior.", exportacion);
        }
    }

    /// <summary>CFDI40130 a CFDI40136: la información global del comprobante a público en general.</summary>
    private static void InformacionGlobal(ContextoValidacion c)
    {
        var raiz = c.Raiz;
        var informacion = raiz.Hijo("InformacionGlobal");

        if (informacion is null)
        {
            if (c.Tipo == "I"
                && c.Receptor?.Valor("Rfc") == ContextoValidacion.RfcGenericoNacional
                && c.Receptor.Valor("Nombre") == "PUBLICO EN GENERAL")
            {
                c.Error(
                    "CFDI40130",
                    raiz,
                    null,
                    "Un ingreso a PUBLICO EN GENERAL (RFC XAXX010101000) debe incluir el nodo InformacionGlobal con la periodicidad, el mes y el año que ampara.");
            }

            return;
        }

        c.EnCatalogo(c.Catalogos.Periodicidades, "CFDI40131", informacion, "Periodicidad", "c_Periodicidad");
        c.EnCatalogo(c.Catalogos.Meses, "CFDI40133", informacion, "Meses", "c_Meses");

        var periodicidad = informacion.Valor("Periodicidad");
        var meses = informacion.Valor("Meses");

        if (periodicidad == "05" && c.Emisor?.Valor("RegimenFiscal") != "621")
        {
            c.Error(
                "CFDI40132",
                informacion,
                "Periodicidad",
                "Con la periodicidad 05 (bimestral) el RegimenFiscal del emisor debe ser 621 (Incorporación Fiscal).",
                c.Emisor?.Valor("RegimenFiscal"),
                "621");
        }

        // Los meses fuera del catálogo ya se informaron con CFDI40133: aquí solo se revisa si cuadran con la periodicidad.
        if (periodicidad is not null && meses is not null && c.Catalogos.Meses.ContainsKey(meses))
        {
            var esBimestre = meses is "13" or "14" or "15" or "16" or "17" or "18";
            if (periodicidad != "05" && esBimestre)
            {
                c.Error("CFDI40134", informacion, "Meses", $"Con la periodicidad {periodicidad} el mes debe ser de 01 a 12, no un bimestre.", meses);
            }
            else if (periodicidad == "05" && !esBimestre)
            {
                c.Error("CFDI40135", informacion, "Meses", "Con la periodicidad 05 (bimestral) el valor de Meses debe ser de 13 a 18.", meses);
            }
        }

        var anioTexto = informacion.Valor("Año");
        if (int.TryParse(anioTexto, out var anio))
        {
            var actual = c.Fecha?.Year ?? c.Ahora.Year;
            if (anio > actual || anio < actual - 5)
            {
                c.Error(
                    "CFDI40136",
                    informacion,
                    "Año",
                    $"El Año debe ser el año del comprobante ({actual}) o uno de los 5 ejercicios anteriores ({actual - 5} a {actual}).",
                    anioTexto);
            }
        }
    }

    /// <summary>CFDI40137: el tipo de relación de cada grupo de CFDI relacionados.</summary>
    private static void Relacionados(ContextoValidacion c)
    {
        foreach (var relacionados in c.Raiz.Hijos("CfdiRelacionados"))
        {
            c.EnCatalogo(c.Catalogos.TiposRelacion, "CFDI40137", relacionados, "TipoRelacion", "c_TipoRelacion");
        }
    }
}
