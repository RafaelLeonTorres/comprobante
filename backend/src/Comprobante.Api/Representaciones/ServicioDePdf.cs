using System.Xml.Linq;
using Comprobante.Api.Seguridad;
using Comprobante.Core.Representacion;
using Comprobante.Pdf;
using Microsoft.Extensions.Options;

namespace Comprobante.Api.Representaciones;

/// <summary>Lo que salió de pedir un PDF.</summary>
internal abstract record ResultadoDePdf
{
    /// <summary>El PDF ya dibujado y el nombre con el que se descarga.</summary>
    public sealed record Listo(byte[] Contenido, string NombreDeArchivo) : ResultadoDePdf;

    /// <summary>El logo no sirve; <paramref name="Error"/> dice por qué, escrito para quien lo subió.</summary>
    public sealed record LogoNoValido(string Error) : ResultadoDePdf;

    /// <summary>Ya se están dibujando tantos PDF como se permiten a la vez.</summary>
    public sealed record Ocupado : ResultadoDePdf;

    /// <summary>El comprobante es válido pero el motor de PDF no pudo dibujarlo.</summary>
    public sealed record NoSePudo : ResultadoDePdf;
}

/// <summary>
/// Dibuja el PDF de un CFDI. Dibujar es trabajo de procesador (un comprobante normal tarda decenas de milisegundos, uno de
/// mil conceptos más de un segundo), así que solo se permiten <c>Limites:PdfSimultaneos</c> a la vez: si ya hay tantos en
/// marcha, la solicitud no espera en fila (se ocuparía un hilo y memoria por cada una), se rechaza y el cliente reintenta.
/// </summary>
internal sealed class ServicioDePdf(
    ConstructorDeRepresentacion constructor, IOptions<LimitesOptions> opciones, ILogger<ServicioDePdf> registro) : IDisposable
{
    private readonly SemaphoreSlim lugares = new(Math.Max(1, opciones.Value.PdfSimultaneos));

    public async Task<ResultadoDePdf> GenerarAsync(
        XDocument xml, byte[]? logo, bool incluirCadenaYSellos, CancellationToken cancelacion)
    {
        if (!await lugares.WaitAsync(TimeSpan.Zero, cancelacion))
        {
            return new ResultadoDePdf.Ocupado();
        }

        try
        {
            if (logo is not null && ValidadorDeLogo.Validar(logo) is { EsValido: false } invalido)
            {
                return new ResultadoDePdf.LogoNoValido(invalido.Error!);
            }

            var representacion = await constructor.ConstruirAsync(xml, cancelacion);
            var configuracion = new OpcionesDelPdf { Logo = logo, IncluirCadenaYSellos = incluirCadenaYSellos };

            // Fuera del hilo de la solicitud y sin cancelación: dibujar no se puede interrumpir a la mitad.
            var contenido = await Task.Run(() => GeneradorPdf.Generar(representacion, configuracion), CancellationToken.None);
            return new ResultadoDePdf.Listo(contenido, representacion.NombreDeArchivo);
        }
        catch (PdfNoGenerableException excepcion)
        {
            registro.LogWarning(excepcion, "No se pudo dibujar el PDF de un comprobante");
            return new ResultadoDePdf.NoSePudo();
        }
        finally
        {
            lugares.Release();
        }
    }

    public void Dispose() => lugares.Dispose();
}
