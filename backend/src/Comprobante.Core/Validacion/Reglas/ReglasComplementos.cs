using System.Xml.Linq;

namespace Comprobante.Core.Validacion.Reglas;

/// <summary>
/// Los complementos (el nodo cfdi:Complemento y el cfdi:ComplementoConcepto de cada concepto) no se revisan: cada uno tiene
/// su propio esquema y sus propias reglas. Se avisa de cada uno para que quien valida sepa qué quedó sin revisar.
/// </summary>
internal static class ReglasComplementos
{
    private const string EspacioTimbre = "http://www.sat.gob.mx/TimbreFiscalDigital";

    public static void Revisar(ContextoValidacion c)
    {
        if (c.Raiz.Hijo("Complemento") is { } complemento)
        {
            foreach (var hijo in complemento.Elements())
            {
                NoRevisado(c, hijo);
            }
        }

        foreach (var concepto in c.Conceptos)
        {
            if (concepto.Hijo("ComplementoConcepto") is { } complementoConcepto)
            {
                foreach (var hijo in complementoConcepto.Elements())
                {
                    NoRevisado(c, hijo);
                }
            }
        }
    }

    private static void NoRevisado(ContextoValidacion c, XElement complemento)
    {
        var mensaje = complemento.Name.NamespaceName == EspacioTimbre
            ? "El Timbre Fiscal Digital no se revisa: esta app no timbra ni comprueba el sello del SAT que agrega el PAC."
            : $"El complemento {complemento.Name.LocalName} ({complemento.Name.NamespaceName}) no se revisa: el comprobante se validó sin las reglas de ese complemento.";
        c.Advertencia(Codigos.ComplementoNoRevisado, complemento, null, mensaje);
    }
}
