using System.Xml.Linq;
using Comprobante.Core.Calculo;

namespace Comprobante.Core.Validacion.Reglas;

/// <summary>
/// Las reglas del resumen de impuestos del comprobante (el nodo cfdi:Impuestos de la matriz de errores del SAT): que cada
/// total, base e importe sea la suma de lo que declaran los conceptos, que no se repita ningún impuesto y que las tasas
/// existan en el catálogo.
/// </summary>
internal static class ReglasImpuestos
{
    /// <summary>La combinación que agrupa los traslados: impuesto, factor y tasa o cuota (los exentos no tienen tasa).</summary>
    private readonly record struct Grupo(string? Impuesto, string? Factor, decimal? TasaOCuota);

    public static void Revisar(ContextoValidacion c)
    {
        var impuestos = c.Raiz.Hijo("Impuestos");

        if (impuestos is not null && c.Tipo is "T" or "P")
        {
            c.Error("CFDI40201", impuestos, null, $"Con el tipo de comprobante {c.Tipo} el nodo Impuestos no debe existir.");
        }

        var trasladosDeConceptos = c.Conceptos.SelectMany(x => x.Hijo("Impuestos").Traslados()).ToList();
        var retencionesDeConceptos = c.Conceptos.SelectMany(x => x.Hijo("Impuestos").Retenciones()).ToList();

        // Si algún dato de los conceptos no es un número, las sumas no significan nada: el esquema ya lo reportó.
        var sumable = trasladosDeConceptos.Concat(retencionesDeConceptos).All(EsSumable);

        Retenciones(c, impuestos, retencionesDeConceptos, sumable);
        Traslados(c, impuestos, trasladosDeConceptos, sumable);
    }

    private static bool EsSumable(XElement nodo) =>
        nodo.Numero("Base") is not null
        && (!nodo.Tiene("Importe") || nodo.Numero("Importe") is not null)
        && (!nodo.Tiene("TasaOCuota") || nodo.Numero("TasaOCuota") is not null);

    /// <summary>CFDI40202, CFDI40203, CFDI40206 a CFDI40211: las retenciones y su total.</summary>
    private static void Retenciones(ContextoValidacion c, XElement? impuestos, List<XElement> deConceptos, bool sumable)
    {
        var contenedor = impuestos?.Hijo("Retenciones");
        var nodos = impuestos.Retenciones().ToList();

        if (impuestos is not null)
        {
            c.DecimalesDeLaMoneda("CFDI40202", impuestos, "TotalImpuestosRetenidos");

            if (impuestos.Numero("TotalImpuestosRetenidos") is { } total && nodos.All(n => n.Numero("Importe") is not null))
            {
                var suma = nodos.Sum(n => n.Numero("Importe")!.Value);
                if (total != suma)
                {
                    c.Error(
                        "CFDI40203",
                        impuestos,
                        "TotalImpuestosRetenidos",
                        $"TotalImpuestosRetenidos ({impuestos.Valor("TotalImpuestosRetenidos")}) debe ser igual a la suma de los importes de las retenciones ({ExtensionesXml.Texto(suma)}).",
                        impuestos.Valor("TotalImpuestosRetenidos"),
                        ExtensionesXml.Texto(suma));
                }
            }

            if (contenedor is not null && !impuestos.Tiene("TotalImpuestosRetenidos"))
            {
                c.Error(
                    "CFDI40206",
                    impuestos,
                    "TotalImpuestosRetenidos",
                    "El comprobante tiene retenciones: el nodo Impuestos debe traer TotalImpuestosRetenidos.");
            }
        }

        foreach (var nodo in nodos)
        {
            c.EnCatalogo(c.Catalogos.Impuestos, "CFDI40207", nodo, "Impuesto", "c_Impuesto");
            c.DecimalesDeLaMoneda("CFDI40210", nodo, "Importe");
        }

        var repetidos = nodos
            .Where(n => n.Valor("Impuesto") is not null)
            .GroupBy(n => n.Valor("Impuesto")!)
            .Where(g => g.Count() > 1)
            .ToList();
        foreach (var grupo in repetidos)
        {
            foreach (var nodo in grupo.Skip(1))
            {
                c.Error(
                    "CFDI40208",
                    nodo,
                    "Impuesto",
                    $"Hay más de una retención del impuesto {grupo.Key}: debe haber un solo registro por cada impuesto retenido.",
                    grupo.Key);
            }
        }

        if (!sumable || c.DecimalesMoneda is not { } decimales)
        {
            return;
        }

        var duplicados = repetidos.Select(g => g.Key).ToHashSet();
        var esperados = deConceptos
            .Where(r => r.Valor("Impuesto") is not null)
            .GroupBy(r => r.Valor("Impuesto")!)
            .ToDictionary(g => g.Key, g => Redondeo.Redondear(g.Sum(r => r.Numero("Importe") ?? 0m), decimales));

        foreach (var nodo in nodos)
        {
            if (nodo.Valor("Impuesto") is not { } impuesto
                || duplicados.Contains(impuesto)
                || nodo.Numero("Importe") is not { } importe)
            {
                continue;
            }

            var esperado = esperados.GetValueOrDefault(impuesto);
            if (importe != esperado)
            {
                c.Error(
                    "CFDI40211",
                    nodo,
                    "Importe",
                    $"El Importe de la retención de {impuesto} ({nodo.Valor("Importe")}) debe ser igual al redondeo de la suma de las retenciones de ese impuesto en los conceptos ({Formato.Fijo(esperado, decimales)}).",
                    nodo.Valor("Importe"),
                    Formato.Fijo(esperado, decimales));
            }
        }

        foreach (var (impuesto, esperado) in esperados)
        {
            if (!nodos.Any(n => n.Valor("Impuesto") == impuesto))
            {
                c.Error(
                    "CFDI40211",
                    impuestos ?? c.Raiz,
                    null,
                    $"Falta la retención de {impuesto} en el resumen de impuestos: los conceptos retienen {Formato.Fijo(esperado, decimales)}.",
                    null,
                    Formato.Fijo(esperado, decimales));
            }
        }
    }

    /// <summary>CFDI40204, CFDI40205, CFDI40212 a CFDI40221: los traslados y su total.</summary>
    private static void Traslados(ContextoValidacion c, XElement? impuestos, List<XElement> deConceptos, bool sumable)
    {
        var nodos = impuestos.Traslados().ToList();

        if (impuestos is not null)
        {
            c.DecimalesDeLaMoneda("CFDI40204", impuestos, "TotalImpuestosTrasladados");

            if (impuestos.Numero("TotalImpuestosTrasladados") is { } total
                && nodos.All(n => !n.Tiene("Importe") || n.Numero("Importe") is not null))
            {
                var suma = nodos.Sum(n => n.Numero("Importe") ?? 0m);
                if (total != suma)
                {
                    c.Error(
                        "CFDI40205",
                        impuestos,
                        "TotalImpuestosTrasladados",
                        $"TotalImpuestosTrasladados ({impuestos.Valor("TotalImpuestosTrasladados")}) debe ser igual a la suma de los importes de los traslados ({ExtensionesXml.Texto(suma)}).",
                        impuestos.Valor("TotalImpuestosTrasladados"),
                        ExtensionesXml.Texto(suma));
                }
            }
        }

        if (deConceptos.Any(t => t.Valor("TipoFactor") != "Exento") && impuestos?.Tiene("TotalImpuestosTrasladados") != true)
        {
            c.Error(
                "CFDI40212",
                impuestos ?? c.Raiz,
                "TotalImpuestosTrasladados",
                "Hay conceptos con impuestos trasladados distintos de Exento: el nodo Impuestos debe traer TotalImpuestosTrasladados.");
        }

        foreach (var nodo in nodos)
        {
            RevisarTraslado(c, nodo);
        }

        var repetidos = nodos.GroupBy(Clave).Where(g => g.Count() > 1).ToList();
        foreach (var grupo in repetidos)
        {
            foreach (var nodo in grupo.Skip(1))
            {
                c.Error(
                    "CFDI40218",
                    nodo,
                    null,
                    $"Hay más de un traslado de {grupo.Key.Impuesto} con factor {grupo.Key.Factor}{DeTasa(grupo.Key)}: debe haber un solo registro por cada combinación de impuesto, factor y tasa.");
            }
        }

        if (!sumable || c.DecimalesMoneda is not { } decimales)
        {
            return;
        }

        var duplicados = repetidos.Select(g => g.Key).ToHashSet();
        var esperados = deConceptos
            .GroupBy(Clave)
            .ToDictionary(
                g => g.Key,
                g => (
                    Base: Redondeo.Redondear(g.Sum(t => t.Numero("Base") ?? 0m), decimales),
                    Importe: Redondeo.Redondear(g.Sum(t => t.Numero("Importe") ?? 0m), decimales)));

        foreach (var nodo in nodos)
        {
            var clave = Clave(nodo);
            if (duplicados.Contains(clave))
            {
                continue;
            }

            var (baseEsperada, importeEsperado) = esperados.GetValueOrDefault(clave);
            var exento = clave.Factor == "Exento";

            if (nodo.Numero("Base") is { } baseResumen && baseResumen != baseEsperada)
            {
                c.Error(
                    exento ? "CFDI40216" : "CFDI40215",
                    nodo,
                    "Base",
                    $"La Base del traslado de {clave.Impuesto} con factor {clave.Factor}{DeTasa(clave)} ({nodo.Valor("Base")}) debe ser igual al redondeo de la suma de las bases de ese traslado en los conceptos ({Formato.Fijo(baseEsperada, decimales)}).",
                    nodo.Valor("Base"),
                    Formato.Fijo(baseEsperada, decimales));
            }

            if (!exento && nodo.Numero("Importe") is { } importeResumen && importeResumen != importeEsperado)
            {
                c.Error(
                    "CFDI40221",
                    nodo,
                    "Importe",
                    $"El Importe del traslado de {clave.Impuesto} con factor {clave.Factor}{DeTasa(clave)} ({nodo.Valor("Importe")}) debe ser igual al redondeo de la suma de los importes de ese traslado en los conceptos ({Formato.Fijo(importeEsperado, decimales)}).",
                    nodo.Valor("Importe"),
                    Formato.Fijo(importeEsperado, decimales));
            }
        }

        var presentes = nodos.Select(Clave).ToHashSet();
        foreach (var (clave, (baseEsperada, importeEsperado)) in esperados)
        {
            if (presentes.Contains(clave))
            {
                continue;
            }

            var exento = clave.Factor == "Exento";
            c.Error(
                exento ? "CFDI40216" : "CFDI40221",
                impuestos ?? c.Raiz,
                null,
                $"Falta en el resumen de impuestos el traslado de {clave.Impuesto} con factor {clave.Factor}{DeTasa(clave)}: los conceptos suman una base de {Formato.Fijo(baseEsperada, decimales)}"
                + (exento ? "." : $" y un importe de {Formato.Fijo(importeEsperado, decimales)}."));
        }
    }

    /// <summary>CFDI40213, CFDI40217 y CFDI40219 en un traslado del resumen.</summary>
    private static void RevisarTraslado(ContextoValidacion c, XElement nodo)
    {
        c.EnCatalogo(c.Catalogos.Impuestos, "CFDI40217", nodo, "Impuesto", "c_Impuesto");
        c.DecimalesDeLaMoneda("CFDI40214", nodo, "Base");
        c.DecimalesDeLaMoneda("CFDI40220", nodo, "Importe");

        var factor = nodo.Valor("TipoFactor");
        if (factor == "Exento")
        {
            if (nodo.Tiene("TasaOCuota") || nodo.Tiene("Importe"))
            {
                c.Error(
                    "CFDI40213",
                    nodo,
                    nodo.Tiene("TasaOCuota") ? "TasaOCuota" : "Importe",
                    "Un traslado exento del resumen solo lleva Base, Impuesto y TipoFactor.");
            }

            return;
        }

        if (factor is not ("Tasa" or "Cuota")
            || nodo.Valor("Impuesto") is not { } impuesto
            || !c.Catalogos.Impuestos.ContainsKey(impuesto))
        {
            return;
        }

        if (!nodo.Tiene("TasaOCuota"))
        {
            c.Error("CFDI40219", nodo, "TasaOCuota", $"Un traslado con factor {factor} debe llevar TasaOCuota.");
        }
        else if (nodo.Numero("TasaOCuota") is { } tasa
            && !c.Catalogos.TasaOCuotaValida(impuesto, factor, tasa, traslado: true, c.FechaDeVigencia))
        {
            c.Error(
                "CFDI40219",
                nodo,
                "TasaOCuota",
                $"El valor {nodo.Valor("TasaOCuota")} no es una {factor.ToLowerInvariant()} del catálogo c_TasaOCuota para trasladar el impuesto {impuesto}, o está fuera de rango.",
                nodo.Valor("TasaOCuota"));
        }
    }

    private static Grupo Clave(XElement traslado)
    {
        var factor = traslado.Valor("TipoFactor");
        return new Grupo(traslado.Valor("Impuesto"), factor, factor == "Exento" ? null : traslado.Numero("TasaOCuota"));
    }

    private static string DeTasa(Grupo grupo) => grupo.TasaOCuota is { } tasa ? $" y tasa o cuota {tasa}" : string.Empty;
}
