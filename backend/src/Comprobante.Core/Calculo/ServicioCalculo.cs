using Comprobante.Core.Catalogos;
using Comprobante.Core.Solicitud;

namespace Comprobante.Core.Calculo;

/// <summary>La vista previa de importes, impuestos y totales: normaliza y revisa la solicitud y la calcula.</summary>
public sealed class ServicioCalculo(ICatalogos catalogos)
{
    public async Task<Resultado<ResultadoCalculo>> CalcularAsync(
        SolicitudCfdi? solicitud, CancellationToken cancelacion = default)
    {
        if (solicitud is null)
        {
            return Resultado<ResultadoCalculo>.Fallo([new("", "Falta el comprobante: el cuerpo de la solicitud está vacío.")]);
        }

        return await CalcularNormalizadaAsync(solicitud.Normalizada(), cancelacion);
    }

    /// <summary>
    /// Lo mismo con la solicitud ya normalizada (<see cref="SolicitudCfdi.Normalizada"/>), para quien después arma el XML con
    /// esos mismos datos, como el generador.
    /// </summary>
    public async Task<Resultado<ResultadoCalculo>> CalcularNormalizadaAsync(
        SolicitudCfdi normalizada, CancellationToken cancelacion = default)
    {
        var pequenos = await catalogos.PequenosAsync(cancelacion);

        var errores = ValidadorSolicitud.Validar(normalizada, pequenos);
        if (errores.Count > 0)
        {
            return Resultado<ResultadoCalculo>.Fallo(errores);
        }

        var calculo = Calculador.Calcular(normalizada, pequenos.Monedas[normalizada.Moneda!].Decimales);
        if (calculo.Total < 0m)
        {
            return Resultado<ResultadoCalculo>.Fallo(
                [new("conceptos", "Las retenciones superan el resto del comprobante: el total no puede ser negativo.")]);
        }

        return Resultado<ResultadoCalculo>.Exito(calculo);
    }
}
