using Comprobante.Core.Calculo;
using Comprobante.Core.Validacion;

namespace Comprobante.Core.Generacion;

/// <summary>Lo que devuelve el generador: el XML sellado con todo lo que se calculó para armarlo, y su revisión.</summary>
/// <param name="Xml">El CFDI 4.0 sellado, listo para guardar como archivo UTF-8. No está timbrado: no tiene validez fiscal.</param>
/// <param name="NombreArchivo">Nombre sugerido para guardarlo; termina en <c>-prueba.xml</c> para que no se confunda con una factura.</param>
/// <param name="CadenaOriginal">El texto que se firmó, calculado con el XSLT del SAT.</param>
/// <param name="Sello">La firma de la cadena original, en Base64 (es el atributo Sello del XML).</param>
/// <param name="NoCertificado">El número del certificado de prueba con el que se firmó.</param>
/// <param name="Calculo">Importes, impuestos y totales de los que salió el XML.</param>
/// <param name="Validacion">El resultado de revisar el XML que se generó con el mismo validador que recibe XML ajenos.</param>
public sealed record ResultadoGeneracion(
    string Xml,
    string NombreArchivo,
    string CadenaOriginal,
    string Sello,
    string NoCertificado,
    ResultadoCalculo Calculo,
    ReporteValidacion Validacion);
