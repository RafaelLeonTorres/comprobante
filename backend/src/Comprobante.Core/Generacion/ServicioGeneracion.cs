using System.Text;
using Comprobante.Core.Calculo;
using Comprobante.Core.Catalogos;
using Comprobante.Core.Documento;
using Comprobante.Core.Sello;
using Comprobante.Core.Solicitud;
using Comprobante.Core.Validacion;
using Comprobante.Core.Xml;

namespace Comprobante.Core.Generacion;

/// <summary>
/// Arma un CFDI 4.0 de ingreso o de egreso a partir de una solicitud: calcula, escribe el XML, lo sella con el CSD de prueba
/// del emisor y lo revisa con el validador. Que el XML tenga hallazgos no es un fallo de esta operación: se entrega junto con
/// su reporte para que se vea qué corregir. Solo falla cuando falta algo para poder armarlo.
/// </summary>
public sealed class ServicioGeneracion(
    ICatalogos catalogos,
    ServicioCalculo calculo,
    EmisoresDePrueba emisores,
    ValidadorCfdi validador,
    TimeProvider reloj)
{
    public async Task<Resultado<ResultadoGeneracion>> GenerarAsync(
        SolicitudCfdi? solicitud, CancellationToken cancelacion = default)
    {
        if (solicitud is null)
        {
            return Resultado<ResultadoGeneracion>.Fallo([new("", "Falta el comprobante: el cuerpo de la solicitud está vacío.")]);
        }

        var (completa, emisor, erroresDelEmisor) = CompletarEmisor(solicitud.Normalizada());

        // El cálculo se hace aunque falle el emisor, para que el usuario vea todos los errores de una vez.
        var resultadoCalculo = await calculo.CalcularNormalizadaAsync(completa, cancelacion);
        var errores = erroresDelEmisor.Concat(resultadoCalculo.Errores).ToList();
        if (emisor is null || errores.Count > 0 || !resultadoCalculo.EsCorrecto)
        {
            return Resultado<ResultadoGeneracion>.Fallo(errores);
        }

        var fecha = await FechaAsync(completa, cancelacion);
        if (!fecha.EsCorrecto)
        {
            return Resultado<ResultadoGeneracion>.Fallo(fecha.Errores);
        }

        var csd = emisores.ObtenerCsd(emisor.Rfc)!;
        var cfdi = ConstructorCfdi.Construir(completa, resultadoCalculo.Valor, csd, fecha.Valor);

        // La cadena original se calcula sobre el XML tal como se va a entregar, sin Sello (el sello no forma parte de ella).
        var cadena = CadenaOriginal.Generar(LectorXmlSeguro.Cargar(EscritorCfdi.Escribir(cfdi)));
        var sello = csd.Firmar(cadena);
        var xml = EscritorCfdi.Escribir(cfdi with { Sello = sello });

        var validacion = await validador.ValidarAsync(xml, cancelacion);
        return Resultado<ResultadoGeneracion>.Exito(new ResultadoGeneracion(
            xml, NombreDeArchivo(cfdi, emisor.Rfc), cadena, sello, csd.NoCertificado, resultadoCalculo.Valor, validacion));
    }

    /// <summary>
    /// Solo se firma con los certificados de prueba del SAT: el emisor tiene que ser uno de ellos. Lo que se omite (nombre,
    /// régimen, código postal del lugar de expedición) se toma de los datos que el SAT tiene registrados para ese RFC.
    /// </summary>
    private (SolicitudCfdi Solicitud, EmisorDePrueba? Emisor, List<ErrorCampo> Errores) CompletarEmisor(SolicitudCfdi solicitud)
    {
        var errores = new List<ErrorCampo>();
        var datos = solicitud.Emisor;
        if (datos?.Rfc is null)
        {
            errores.Add(new(datos is null ? "emisor" : "emisor.rfc", "Falta el RFC del emisor: elige uno de los emisores de prueba."));
            return (solicitud, null, errores);
        }

        var emisor = emisores.Buscar(datos.Rfc);
        if (emisor is null)
        {
            errores.Add(new("emisor.rfc",
                $"El RFC {datos.Rfc} no es de un certificado de prueba del SAT. Esta app solo firma con esos diez: "
                + "elige uno de la lista de emisores de prueba."));
            return (solicitud, null, errores);
        }

        var completa = solicitud with
        {
            Emisor = datos with
            {
                Rfc = emisor.Rfc,
                Nombre = datos.Nombre ?? emisor.Nombre,
                RegimenFiscal = datos.RegimenFiscal ?? emisor.RegimenFiscal,
            },
            LugarExpedicion = solicitud.LugarExpedicion ?? emisor.CodigoPostal,
        };
        return (completa, emisor, errores);
    }

    /// <summary>
    /// El atributo Fecha: la que indicó el usuario o, si la omitió, la hora actual del lugar de expedición (la zona la da el
    /// huso del código postal en el catálogo).
    /// </summary>
    private async Task<Resultado<string>> FechaAsync(SolicitudCfdi solicitud, CancellationToken cancelacion)
    {
        if (solicitud.Fecha is { } indicada)
        {
            return Resultado<string>.Exito(ZonaHoraria.ComoFecha(indicada));
        }

        var codigo = solicitud.LugarExpedicion!;
        var encontrados = await catalogos.CodigosPostalesAsync([codigo], cancelacion);
        if (!encontrados.TryGetValue(codigo, out var postal))
        {
            return Resultado<string>.Fallo([new("lugarExpedicion",
                $"Sin fecha no se puede poner la hora local: el código postal {codigo} no está en el catálogo c_CodigoPostal. "
                + "Indica la fecha o corrige el código postal.")]);
        }

        var local = ZonaHoraria.Local(reloj.GetUtcNow(), postal.HusoHorario);
        return Resultado<string>.Exito(ZonaHoraria.ComoFecha(local));
    }

    /// <summary>
    /// <c>RFC-Serie-Folio-prueba.xml</c>; sin serie ni folio, la fecha en su lugar. Solo letras ASCII, números, guion y guion
    /// bajo: lo demás (el <c>&amp;</c> y la Ñ de algunos RFC, la barra de un folio) pasa a guion bajo, porque el nombre viaja
    /// en cabeceras y en rutas.
    /// </summary>
    private static string NombreDeArchivo(Cfdi cfdi, string rfc)
    {
        var identificador = string.Join('-', new[] { cfdi.Serie, cfdi.Folio }.Where(parte => !string.IsNullOrEmpty(parte)));
        if (identificador.Length == 0)
        {
            identificador = cfdi.Fecha!.Replace("-", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal);
        }

        var nombre = new StringBuilder(rfc).Append('-').Append(identificador);
        for (var i = 0; i < nombre.Length; i++)
        {
            if (!(char.IsAsciiLetterOrDigit(nombre[i]) || nombre[i] is '-' or '_'))
            {
                nombre[i] = '_';
            }
        }

        // Un folio de 40 caracteres y una serie de 25 caben de sobra; esto solo evita nombres absurdos.
        return (nombre.Length > 80 ? nombre.ToString(0, 80) : nombre.ToString()) + "-prueba.xml";
    }
}
