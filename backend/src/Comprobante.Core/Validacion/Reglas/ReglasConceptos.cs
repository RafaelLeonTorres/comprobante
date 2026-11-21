using System.Xml.Linq;
using Comprobante.Core.Calculo;

namespace Comprobante.Core.Validacion.Reglas;

/// <summary>
/// Las reglas de cada concepto de la matriz de errores del SAT: claves de los catálogos, importes dentro de sus límites,
/// objeto de impuesto frente al nodo Impuestos, los impuestos de cada concepto, las partes, la información aduanera y el nodo
/// ACuentaTerceros.
/// </summary>
internal static class ReglasConceptos
{
    internal const string RfcExtranjeroTerceros = "EXT990101NI1";

    internal const string NombreExtranjeroTerceros =
        "EXPEDICIÓN DE CFDI POR RESIDENTES EN MÉXICO QUE PRESTAN SERVICIOS DE INTERMEDIACIÓN ENTRE TERCEROS A OFERENTES DE BIENES Y SERVICIOS RESIDENTES EN EL EXTRANJERO";

    private static readonly string[] ClavesDeHidrocarburos = ["15101505", "15101514", "15101515"];

    public static void Revisar(ContextoValidacion c)
    {
        foreach (var concepto in c.Conceptos)
        {
            Claves(c, concepto);
            Importes(c, concepto);
            ObjetoDeImpuesto(c, concepto);
            foreach (var traslado in concepto.Hijo("Impuestos").Traslados())
            {
                ImpuestoDeConcepto(c, traslado, esTraslado: true);
            }

            foreach (var retencion in concepto.Hijo("Impuestos").Retenciones())
            {
                ImpuestoDeConcepto(c, retencion, esTraslado: false);
            }

            ACuentaDeTerceros(c, concepto);
            InformacionAduanera(c, concepto);
            Partes(c, concepto);
        }
    }

    /// <summary>CFDI40162, CFDI40164, CFDI40165 y CFDI140228: claves de producto o servicio y de unidad.</summary>
    private static void Claves(ContextoValidacion c, XElement concepto)
    {
        if (c.EnCatalogo(c.ClavesProdServ, "CFDI40162", concepto, "ClaveProdServ", "c_ClaveProdServ"))
        {
            var clave = concepto.Valor("ClaveProdServ")!;
            var registro = c.ClavesProdServ[clave];
            var traslados = concepto.Hijo("Impuestos").Traslados().ToList();

            // El catálogo marca con "Sí" las claves cuyo concepto debe desglosar IVA o IEPS trasladado.
            if (concepto.Valor("ObjetoImp") == "02")
            {
                if (registro.IncluirIva == "Sí" && !traslados.Any(t => t.Valor("Impuesto") == "002"))
                {
                    c.Error(
                        "CFDI40164",
                        concepto,
                        "ClaveProdServ",
                        $"La clave {clave} ({registro.Descripcion}) debe llevar IVA trasladado (002) y el concepto no lo declara.",
                        clave);
                }

                if (registro.IncluirIeps == "Sí" && !traslados.Any(t => t.Valor("Impuesto") == "003"))
                {
                    c.Error(
                        "CFDI40164",
                        concepto,
                        "ClaveProdServ",
                        $"La clave {clave} ({registro.Descripcion}) debe llevar IEPS trasladado (003) y el concepto no lo declara.",
                        clave);
                }
            }

            if (c.Tipo is "I" or "E"
                && ClavesDeHidrocarburos.Contains(clave)
                && !TieneComplementoDeHidrocarburos(concepto))
            {
                c.Error(
                    "CFDI140228",
                    concepto,
                    "ClaveProdServ",
                    $"Con la clave {clave} en un comprobante de ingreso o egreso, el concepto debe incluir el complemento de hidrocarburos y petrolíferos (cfdi:ComplementoConcepto).",
                    clave);
            }
        }

        c.EnCatalogo(c.ClavesUnidad, "CFDI40165", concepto, "ClaveUnidad", "c_ClaveUnidad");
    }

    private static bool TieneComplementoDeHidrocarburos(XElement concepto) =>
        concepto.Hijo("ComplementoConcepto")?.Elements()
            .Any(e => e.Name.NamespaceName == "http://www.sat.gob.mx/hidrocarburospetroliferos") ?? false;

    /// <summary>CFDI40166 a CFDI40169: valor unitario, importe y descuento del concepto.</summary>
    private static void Importes(ContextoValidacion c, XElement concepto)
    {
        if (c.EsIngresoEgresoONomina && concepto.Numero("ValorUnitario") is <= 0m)
        {
            c.Error(
                "CFDI40166",
                concepto,
                "ValorUnitario",
                "Con el tipo de comprobante I, E o N el ValorUnitario del concepto debe ser mayor que cero.",
                concepto.Valor("ValorUnitario"));
        }

        var importe = concepto.Numero("Importe");
        if (LimitesDelImporte(concepto) is { } rango && importe is { } valor && !rango.Contiene(valor))
        {
            var decimales = concepto.Decimales("Importe");
            c.Error(
                "CFDI40167",
                concepto,
                "Importe",
                $"El Importe ({concepto.Valor("Importe")}) debe estar entre {Formato.Fijo(rango.Inferior, decimales)} y {Formato.Fijo(rango.Superior, decimales)}, que son los límites que salen de Cantidad por ValorUnitario.",
                concepto.Valor("Importe"),
                $"{Formato.Fijo(rango.Inferior, decimales)} a {Formato.Fijo(rango.Superior, decimales)}");
        }

        if (concepto.Tiene("Descuento"))
        {
            if (concepto.Decimales("Descuento") > concepto.Decimales("Importe"))
            {
                c.Error(
                    "CFDI40168",
                    concepto,
                    "Descuento",
                    $"El Descuento trae más decimales ({concepto.Decimales("Descuento")}) que el Importe del concepto ({concepto.Decimales("Importe")}).",
                    concepto.Valor("Descuento"));
            }

            if (concepto.Numero("Descuento") is { } descuento && importe is { } tope && descuento > tope)
            {
                c.Error(
                    "CFDI40169",
                    concepto,
                    "Descuento",
                    $"El Descuento ({concepto.Valor("Descuento")}) no puede ser mayor que el Importe del concepto ({concepto.Valor("Importe")}).",
                    concepto.Valor("Descuento"));
            }
        }
    }

    /// <summary>Los límites del Importe de un concepto o de una parte; nulo si falta algún dato o las cifras no caben en un decimal.</summary>
    private static Limites.Rango? LimitesDelImporte(XElement nodo) =>
        nodo.Numero("Cantidad") is { } cantidad && nodo.Numero("ValorUnitario") is { } valorUnitario
            ? Limites.ImporteConcepto(
                cantidad,
                nodo.Decimales("Cantidad"),
                valorUnitario,
                nodo.Decimales("ValorUnitario"),
                nodo.Decimales("Importe"))
            : null;

    /// <summary>CFDI40170 a CFDI40173, CFDI140226 y CFDI140227: el objeto de impuesto y el nodo Impuestos del concepto.</summary>
    private static void ObjetoDeImpuesto(ContextoValidacion c, XElement concepto)
    {
        c.EnCatalogo(c.Catalogos.ObjetosImp, "CFDI40170", concepto, "ObjetoImp", "c_ObjetoImp");

        var objeto = concepto.Valor("ObjetoImp");
        var impuestos = concepto.Hijo("Impuestos");

        if (objeto == "02" && impuestos is null)
        {
            c.Error(
                "CFDI40171",
                concepto,
                "ObjetoImp",
                "Con ObjetoImp 02 (sí objeto de impuesto) el concepto debe incluir el nodo Impuestos.",
                objeto);
        }

        if (impuestos is not null && objeto is "01" or "03" or "04" or "05")
        {
            c.Error(
                "CFDI40172",
                impuestos,
                null,
                $"Con ObjetoImp {objeto} el concepto no lleva el nodo Impuestos: quítalo o cambia el ObjetoImp a 02.");
        }

        if (impuestos is not null && impuestos.Hijo("Traslados") is null && impuestos.Hijo("Retenciones") is null)
        {
            c.Error("CFDI40173", impuestos, null, "El nodo Impuestos del concepto debe incluir traslados, retenciones o ambos.");
        }

        var traslados = impuestos.Traslados().ToList();
        var retenciones = impuestos.Retenciones().ToList();
        switch (objeto)
        {
            case "06" or "08":
                foreach (var nodo in traslados.Concat(retenciones).Where(n => n.Valor("Impuesto") is "002" or "003"))
                {
                    c.Error(
                        "CFDI140226",
                        nodo,
                        "Impuesto",
                        $"Con ObjetoImp {objeto} el concepto no puede llevar IVA (002) ni IEPS (003), solo retenciones de ISR (001).",
                        nodo.Valor("Impuesto"));
                }

                break;
            case "07":
                foreach (var nodo in traslados.Concat(retenciones).Where(n => n.Valor("Impuesto") == "002"))
                {
                    c.Error(
                        "CFDI140227",
                        nodo,
                        "Impuesto",
                        "Con ObjetoImp 07 el concepto no puede llevar IVA (002).",
                        nodo.Valor("Impuesto"));
                }

                if (!traslados.Any(t => t.Valor("Impuesto") == "003"))
                {
                    c.Error(
                        "CFDI140227",
                        concepto,
                        "ObjetoImp",
                        "Con ObjetoImp 07 el concepto debe desglosar un traslado de IEPS (003).",
                        objeto);
                }

                break;
        }
    }

    /// <summary>
    /// CFDI40174 a CFDI40186: un traslado o una retención del concepto. Los dos comparten casi todo; cambian el código del SAT
    /// y que el traslado exige tasa e importe salvo si es exento, mientras que la retención no puede ser exenta.
    /// </summary>
    private static void ImpuestoDeConcepto(ContextoValidacion c, XElement nodo, bool esTraslado)
    {
        var (codigoBase, codigoImpuesto, codigoFactor, codigoTasa, codigoImporte) = esTraslado
            ? ("CFDI40174", "CFDI40175", "CFDI40176", "CFDI40179", "CFDI40180")
            : ("CFDI40181", "CFDI40182", "CFDI40183", "CFDI40185", "CFDI40186");
        var cual = esTraslado ? "traslado" : "retención";

        if (nodo.Numero("Base") is <= 0m)
        {
            c.Error(codigoBase, nodo, "Base", $"La Base del {cual} debe ser mayor que cero.", nodo.Valor("Base"));
        }

        var impuestoValido = c.EnCatalogo(c.Catalogos.Impuestos, codigoImpuesto, nodo, "Impuesto", "c_Impuesto");
        c.EnCatalogo(c.Catalogos.TiposFactor, codigoFactor, nodo, "TipoFactor", "c_TipoFactor");

        var factor = nodo.Valor("TipoFactor");
        if (esTraslado)
        {
            if (factor == "Exento" && (nodo.Tiene("TasaOCuota") || nodo.Tiene("Importe")))
            {
                c.Error(
                    "CFDI40177",
                    nodo,
                    nodo.Tiene("TasaOCuota") ? "TasaOCuota" : "Importe",
                    "Un traslado exento no lleva TasaOCuota ni Importe.");
            }
            else if (factor is "Tasa" or "Cuota" && (!nodo.Tiene("TasaOCuota") || !nodo.Tiene("Importe")))
            {
                c.Error(
                    "CFDI40178",
                    nodo,
                    nodo.Tiene("TasaOCuota") ? "Importe" : "TasaOCuota",
                    $"Un traslado con factor {factor} debe llevar TasaOCuota e Importe.");
            }
        }
        else if (factor == "Exento")
        {
            c.Error("CFDI40184", nodo, "TipoFactor", "Una retención no puede ser exenta: el TipoFactor debe ser Tasa o Cuota.", factor);
        }

        if (factor is not ("Tasa" or "Cuota"))
        {
            return;
        }

        var tasa = nodo.Numero("TasaOCuota");
        if (impuestoValido && tasa is { } valor
            && !c.Catalogos.TasaOCuotaValida(nodo.Valor("Impuesto")!, factor, valor, esTraslado, c.FechaDeVigencia))
        {
            c.Error(
                codigoTasa,
                nodo,
                "TasaOCuota",
                $"El valor {nodo.Valor("TasaOCuota")} no es una {factor.ToLowerInvariant()} del catálogo c_TasaOCuota para {(esTraslado ? "trasladar" : "retener")} el impuesto {nodo.Valor("Impuesto")}, o está fuera de rango.",
                nodo.Valor("TasaOCuota"));
        }

        if (nodo.Numero("Base") is { } baseImpuesto && tasa is { } tasaLeida && nodo.Numero("Importe") is { } importe
            && Limites.ImporteImpuesto(baseImpuesto, nodo.Decimales("Base"), tasaLeida, nodo.Decimales("Importe")) is { } rango
            && !rango.Contiene(importe))
        {
            var decimales = nodo.Decimales("Importe");
            c.Error(
                codigoImporte,
                nodo,
                "Importe",
                $"El Importe del {cual} ({nodo.Valor("Importe")}) debe estar entre {Formato.Fijo(rango.Inferior, decimales)} y {Formato.Fijo(rango.Superior, decimales)}, que son los límites que salen de Base por TasaOCuota.",
                nodo.Valor("Importe"),
                $"{Formato.Fijo(rango.Inferior, decimales)} a {Formato.Fijo(rango.Superior, decimales)}");
        }
    }

    /// <summary>CFDI40188, CFDI40189, CFDI40191, CFDI40193 y CFDI140223: el nodo ACuentaTerceros.</summary>
    private static void ACuentaDeTerceros(ContextoValidacion c, XElement concepto)
    {
        if (concepto.Hijo("ACuentaTerceros") is not { } terceros)
        {
            return;
        }

        var rfc = terceros.Valor("RfcACuentaTerceros");
        if (rfc is not null && (rfc == c.Emisor?.Valor("Rfc") || rfc == c.Receptor?.Valor("Rfc")))
        {
            c.Error(
                "CFDI40188",
                terceros,
                "RfcACuentaTerceros",
                "El RFC del tercero debe ser distinto del RFC del emisor y del receptor.",
                rfc);
        }

        c.EnCatalogo(c.Catalogos.RegimenesFiscales, "CFDI40191", terceros, "RegimenFiscalACuentaTerceros", "c_RegimenFiscal");

        if (rfc != RfcExtranjeroTerceros)
        {
            return;
        }

        if (terceros.Valor("NombreACuentaTerceros") is { } nombre
            && !nombre.Contains(NombreExtranjeroTerceros, StringComparison.OrdinalIgnoreCase))
        {
            c.Error(
                "CFDI40189",
                terceros,
                "NombreACuentaTerceros",
                $"Con el RFC {RfcExtranjeroTerceros} el nombre del tercero debe ser \"{NombreExtranjeroTerceros}\".",
                nombre,
                NombreExtranjeroTerceros);
        }

        if (terceros.Valor("RegimenFiscalACuentaTerceros") is { } regimen && regimen != "616")
        {
            c.Error(
                "CFDI140223",
                terceros,
                "RegimenFiscalACuentaTerceros",
                $"Con el RFC {RfcExtranjeroTerceros} el régimen fiscal del tercero debe ser 616.",
                regimen,
                "616");
        }

        if (terceros.Valor("DomicilioFiscalACuentaTerceros") is { } domicilio && domicilio != c.Raiz.Valor("LugarExpedicion"))
        {
            c.Error(
                "CFDI40193",
                terceros,
                "DomicilioFiscalACuentaTerceros",
                $"Con el RFC {RfcExtranjeroTerceros} el domicilio fiscal del tercero debe ser igual al LugarExpedicion del comprobante.",
                domicilio,
                c.Raiz.Valor("LugarExpedicion"));
        }
    }

    /// <summary>CFDI40195: con el complemento de Comercio Exterior el concepto no lleva número de pedimento.</summary>
    private static void InformacionAduanera(ContextoValidacion c, XElement concepto)
    {
        if (!c.TieneComercioExterior)
        {
            return;
        }

        foreach (var aduanera in concepto.Hijos("InformacionAduanera").Where(a => a.Tiene("NumeroPedimento")))
        {
            c.Error(
                "CFDI40195",
                aduanera,
                "NumeroPedimento",
                "Con el complemento de Comercio Exterior el concepto no lleva NumeroPedimento.",
                aduanera.Valor("NumeroPedimento"));
        }
    }

    /// <summary>CFDI40196 a CFDI40198 y CFDI40200: las partes de un concepto.</summary>
    private static void Partes(ContextoValidacion c, XElement concepto)
    {
        foreach (var parte in concepto.Hijos("Parte"))
        {
            c.EnCatalogo(c.ClavesProdServ, "CFDI40196", parte, "ClaveProdServ", "c_ClaveProdServ");

            if (parte.Numero("ValorUnitario") is <= 0m)
            {
                c.Error("CFDI40197", parte, "ValorUnitario", "El ValorUnitario de la parte debe ser mayor que cero.", parte.Valor("ValorUnitario"));
            }

            if (LimitesDelImporte(parte) is { } rango && parte.Numero("Importe") is { } importe && !rango.Contiene(importe))
            {
                var decimales = parte.Decimales("Importe");
                c.Error(
                    "CFDI40198",
                    parte,
                    "Importe",
                    $"El Importe de la parte ({parte.Valor("Importe")}) debe estar entre {Formato.Fijo(rango.Inferior, decimales)} y {Formato.Fijo(rango.Superior, decimales)}, que son los límites que salen de Cantidad por ValorUnitario.",
                    parte.Valor("Importe"),
                    $"{Formato.Fijo(rango.Inferior, decimales)} a {Formato.Fijo(rango.Superior, decimales)}");
            }

            if (!c.TieneComercioExterior)
            {
                continue;
            }

            foreach (var aduanera in parte.Hijos("InformacionAduanera").Where(a => a.Tiene("NumeroPedimento")))
            {
                c.Error(
                    "CFDI40200",
                    aduanera,
                    "NumeroPedimento",
                    "Con el complemento de Comercio Exterior la parte no lleva NumeroPedimento.",
                    aduanera.Valor("NumeroPedimento"));
            }
        }
    }
}
