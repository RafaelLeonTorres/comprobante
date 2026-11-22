using System.Globalization;
using System.Text;
using QRCoder;

namespace Comprobante.Pdf;

/// <summary>
/// El código QR de verificación del SAT, dibujado como vectores (SVG): nítido a cualquier tamaño y al imprimir, sin
/// imágenes de por medio que un visor pueda suavizar.
/// </summary>
internal static class CodigoQr
{
    /// <summary>
    /// El QR de ese texto, negro sobre transparente y sin zona de silencio (la deja la plantilla alrededor). Nivel de
    /// corrección M, el que usan los CFDI timbrados.
    /// </summary>
    public static string ComoSvg(string texto)
    {
        var modulos = Modulos(texto);
        var lado = modulos.GetLength(0);

        var trazo = new StringBuilder();
        for (var y = 0; y < lado; y++)
        {
            var x = 0;
            while (x < lado)
            {
                if (!modulos[y, x])
                {
                    x++;
                    continue;
                }

                var inicio = x;
                while (x < lado && modulos[y, x])
                {
                    x++;
                }

                var largo = x - inicio;
                trazo.Append(CultureInfo.InvariantCulture, $"M{inicio} {y}h{largo}v1h-{largo}z");
            }
        }

        var medida = lado.ToString(CultureInfo.InvariantCulture);
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {medida} {medida}\" shape-rendering=\"crispEdges\">"
            + $"<path fill=\"#000000\" d=\"{trazo}\"/></svg>";
    }

    /// <summary>La matriz de módulos del QR (verdadero = oscuro), sin la zona de silencio.</summary>
    public static bool[,] Modulos(string texto)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);

        // QRCoder agrega 4 módulos de zona de silencio por lado: aquí no hacen falta.
        const int silencio = 4;
        var matriz = datos.ModuleMatrix;
        var lado = matriz.Count - (2 * silencio);
        var resultado = new bool[lado, lado];
        for (var y = 0; y < lado; y++)
        {
            for (var x = 0; x < lado; x++)
            {
                resultado[y, x] = matriz[y + silencio][x + silencio];
            }
        }

        return resultado;
    }
}
