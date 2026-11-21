using System.Text.RegularExpressions;
using System.Xml.Linq;
using Comprobante.Core.Catalogos;

namespace Comprobante.Core.Validacion.Reglas;

/// <summary>
/// Las reglas del emisor y del receptor de la matriz de errores del SAT: régimen fiscal frente al tipo de persona, RFC
/// genérico, residencia fiscal, número de registro de identidad tributaria y uso del CFDI. Las que comparan nombre, RFC o
/// domicilio con la lista de RFC inscritos del SAT no se pueden revisar sin conexión (ver <see cref="ReglasSat"/>).
/// </summary>
internal static class ReglasPartes
{
    public const string NombrePublicoEnGeneral = "PUBLICO EN GENERAL";

    public static void Revisar(ContextoValidacion c)
    {
        if (c.Emisor is { } emisor)
        {
            Emisor(c, emisor);
        }

        if (c.Receptor is { } receptor)
        {
            Receptor(c, receptor);
        }
    }

    /// <summary>CFDI40140 y CFDI40141: el régimen fiscal existe y es de la persona que emite.</summary>
    private static void Emisor(ContextoValidacion c, XElement emisor)
    {
        if (c.EnCatalogo(c.Catalogos.RegimenesFiscales, "CFDI40140", emisor, "RegimenFiscal", "c_RegimenFiscal"))
        {
            var regimen = c.Catalogos.RegimenesFiscales[emisor.Valor("RegimenFiscal")!];
            PersonaCorresponde(c, "CFDI40141", emisor, "RegimenFiscal", "emisor", regimen);
        }
    }

    private static void Receptor(ContextoValidacion c, XElement receptor)
    {
        var rfc = receptor.Valor("Rfc");
        var generico = ContextoValidacion.EsRfcGenerico(rfc);

        if (receptor.Valor("Nombre") == NombrePublicoEnGeneral && rfc != ContextoValidacion.RfcGenericoNacional)
        {
            c.Error(
                "CFDI40146",
                receptor,
                "Rfc",
                $"Con el nombre {NombrePublicoEnGeneral} el RFC del receptor debe ser {ContextoValidacion.RfcGenericoNacional}.",
                rfc,
                ContextoValidacion.RfcGenericoNacional);
        }

        if (generico && receptor.Valor("DomicilioFiscalReceptor") is { } domicilio && domicilio != c.Raiz.Valor("LugarExpedicion"))
        {
            c.Error(
                "CFDI40149",
                receptor,
                "DomicilioFiscalReceptor",
                "Con un RFC genérico el DomicilioFiscalReceptor debe ser igual al LugarExpedicion del comprobante.",
                domicilio,
                c.Raiz.Valor("LugarExpedicion"));
        }

        ResidenciaYRegistro(c, receptor, rfc);
        RegimenYUso(c, receptor, rfc, generico);
    }

    /// <summary>CFDI40150 a CFDI40156: residencia fiscal y número de registro de identidad tributaria (extranjeros).</summary>
    private static void ResidenciaYRegistro(ContextoValidacion c, XElement receptor, string? rfc)
    {
        var residencia = receptor.Valor("ResidenciaFiscal");
        var registro = receptor.Valor("NumRegIdTrib");
        var esExtranjero = rfc == ContextoValidacion.RfcGenericoExtranjero;

        if (residencia is not null && !c.Catalogos.Paises.ContainsKey(residencia))
        {
            c.Error("CFDI40150", receptor, "ResidenciaFiscal", $"La clave {residencia} no está en el catálogo c_Pais del SAT.", residencia);
        }

        // Solo el RFC genérico extranjero lleva residencia fiscal; con uno nacional (inscrito o genérico) el atributo no existe.
        if (!esExtranjero && residencia is not null)
        {
            c.Error(
                "CFDI40151",
                receptor,
                "ResidenciaFiscal",
                "El receptor tiene un RFC nacional: solo el RFC genérico extranjero (XEXX010101000) lleva ResidenciaFiscal. Quita el atributo.",
                residencia);
        }
        else if (residencia == "MEX")
        {
            c.Error("CFDI40152", receptor, "ResidenciaFiscal", "La ResidenciaFiscal de un receptor extranjero no puede ser MEX.", residencia);
        }

        if (esExtranjero && residencia is null && (c.TieneComercioExterior || registro is not null))
        {
            c.Error(
                "CFDI40153",
                receptor,
                null,
                "Con el RFC genérico extranjero, el complemento de Comercio Exterior o un NumRegIdTrib exigen registrar la ResidenciaFiscal (país del catálogo c_Pais).");
        }

        if (!esExtranjero && registro is not null)
        {
            c.Error(
                "CFDI40154",
                receptor,
                "NumRegIdTrib",
                "El receptor tiene un RFC nacional: solo el RFC genérico extranjero (XEXX010101000) lleva NumRegIdTrib. Quita el atributo.",
                registro);
        }

        if (esExtranjero && c.TieneComercioExterior && registro is null)
        {
            c.Error(
                "CFDI40155",
                receptor,
                null,
                "Con el RFC genérico extranjero y el complemento de Comercio Exterior, el receptor debe registrar NumRegIdTrib.");
        }

        if (registro is not null
            && residencia is not null
            && c.Catalogos.Paises.TryGetValue(residencia, out var pais)
            && !string.IsNullOrEmpty(pais.FormatoRegistroIdentidadTributaria)
            && !CumpleFormato(registro, pais.FormatoRegistroIdentidadTributaria))
        {
            c.Error(
                "CFDI40156",
                receptor,
                "NumRegIdTrib",
                $"El NumRegIdTrib no cumple con el formato que el catálogo c_Pais fija para {pais.Descripcion}: {pais.FormatoRegistroIdentidadTributaria}.",
                registro,
                pais.FormatoRegistroIdentidadTributaria);
        }
    }

    /// <summary>CFDI40157 a CFDI40161: el régimen del receptor y el uso del CFDI que le corresponden.</summary>
    private static void RegimenYUso(ContextoValidacion c, XElement receptor, string? rfc, bool generico)
    {
        var regimenClave = receptor.Valor("RegimenFiscalReceptor");
        RegimenFiscal? regimen = null;
        if (c.EnCatalogo(c.Catalogos.RegimenesFiscales, "CFDI40157", receptor, "RegimenFiscalReceptor", "c_RegimenFiscal"))
        {
            regimen = c.Catalogos.RegimenesFiscales[regimenClave!];
            PersonaCorresponde(c, "CFDI40158", receptor, "RegimenFiscalReceptor", "receptor", regimen);
        }

        if (generico && regimenClave is not null && regimenClave != "616")
        {
            c.Error(
                "CFDI40159",
                receptor,
                "RegimenFiscalReceptor",
                "Con un RFC genérico (XAXX010101000 o XEXX010101000) el RegimenFiscalReceptor debe ser 616 (Sin obligaciones fiscales).",
                regimenClave,
                "616");
        }

        var usoClave = receptor.Valor("UsoCFDI");
        if (usoClave is null || !c.EnCatalogo(c.Catalogos.UsosCfdi, "CFDI40160", receptor, "UsoCFDI", "c_UsoCFDI"))
        {
            return;
        }

        var uso = c.Catalogos.UsosCfdi[usoClave];
        var esMoral = ContextoValidacion.EsPersonaMoral(rfc);
        if (esMoral is { } moral && !(moral ? uso.Moral : uso.Fisica))
        {
            c.Error(
                "CFDI40161",
                receptor,
                "UsoCFDI",
                $"El uso {usoClave} ({uso.Descripcion.TrimEnd('.')}) no aplica a personas {(moral ? "morales" : "físicas")}, y el RFC del receptor es de persona {(moral ? "moral" : "física")}.",
                usoClave);
        }
        else if (regimen is not null && !uso.RegimenesReceptor.Contains(regimen.Clave))
        {
            c.Error(
                "CFDI40161",
                receptor,
                "UsoCFDI",
                $"El uso {usoClave} ({uso.Descripcion.TrimEnd('.')}) no se puede usar con el régimen fiscal {regimen.Clave} del receptor: el catálogo c_UsoCFDI lo permite con los regímenes {string.Join(", ", uso.RegimenesReceptor)}.",
                usoClave);
        }
    }

    /// <summary>Anota el hallazgo si el régimen no aplica al tipo de persona que indica la longitud del RFC.</summary>
    private static void PersonaCorresponde(
        ContextoValidacion c, string codigo, XElement nodo, string atributo, string quien, RegimenFiscal regimen)
    {
        var rfc = nodo.Valor("Rfc");
        if (ContextoValidacion.EsPersonaMoral(rfc) is not { } moral || (moral ? regimen.Moral : regimen.Fisica))
        {
            return;
        }

        c.Error(
            codigo,
            nodo,
            atributo,
            $"El régimen fiscal {regimen.Clave} ({regimen.Descripcion.TrimEnd('.')}) no aplica a personas {(moral ? "morales" : "físicas")}, y el RFC del {quien} es de persona {(moral ? "moral" : "física")}.",
            regimen.Clave);
    }

    /// <summary>Si el texto cumple el patrón completo; un patrón del catálogo que no se pueda leer no rechaza nada.</summary>
    private static bool CumpleFormato(string texto, string patron)
    {
        try
        {
            return Regex.IsMatch(texto, $"^(?:{patron})$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            return true;
        }
    }
}
