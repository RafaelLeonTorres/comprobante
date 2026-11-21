using Comprobante.Core.Calculo;
using Comprobante.Core.Catalogos;

namespace Comprobante.Core.Solicitud;

/// <summary>
/// Revisa que la solicitud se pueda calcular y armar: lo que, si faltara, impediría escribir el XML (la moneda, los
/// importes, los factores de los impuestos). Que las claves existan en los catálogos, que el régimen cuadre con el
/// tipo de persona o que el uso del CFDI sea el correcto lo revisa el validador sobre el XML ya armado, con el código
/// de error del SAT de cada regla. Espera la solicitud ya normalizada (<see cref="SolicitudCfdi.Normalizada"/>).
/// </summary>
public static class ValidadorSolicitud
{
    public const int MaximoConceptos = 500;

    /// <summary>Cantidad y valor unitario deben ser menores que esto (doce cifras enteras).</summary>
    public const decimal LimiteCantidad = 1_000_000_000_000m;

    /// <summary>
    /// El importe de un concepto debe ser menor que esto. Con quince cifras enteras y los doce decimales del producto de
    /// dos números de seis decimales, el cálculo cabe exacto en un decimal (28 cifras).
    /// </summary>
    public const decimal LimiteImporte = 1_000_000_000_000_000m;

    public static IReadOnlyList<ErrorCampo> Validar(SolicitudCfdi solicitud, CatalogosPequenos catalogos)
    {
        var errores = new List<ErrorCampo>();

        if (solicitud.TipoDeComprobante is not ("I" or "E"))
        {
            errores.Add(new("tipoDeComprobante", "Esta app arma comprobantes de ingreso (I) y de egreso (E)."));
        }

        ValidarMoneda(errores, solicitud, catalogos);

        var conceptos = solicitud.Conceptos ?? [];
        if (conceptos.Count == 0)
        {
            errores.Add(new("conceptos", "Agrega al menos un concepto."));
        }
        else if (conceptos.Count > MaximoConceptos)
        {
            errores.Add(new("conceptos", $"Son demasiados conceptos: el máximo es {MaximoConceptos}."));
        }
        else
        {
            for (var i = 0; i < conceptos.Count; i++)
            {
                ValidarConcepto(errores, $"conceptos[{i}]", conceptos[i], catalogos);
            }
        }

        return errores;
    }

    private static void ValidarMoneda(List<ErrorCampo> errores, SolicitudCfdi solicitud, CatalogosPequenos catalogos)
    {
        var moneda = solicitud.Moneda ?? "MXN";
        if (!catalogos.Monedas.ContainsKey(moneda))
        {
            errores.Add(new("moneda", $"La moneda {moneda} no está en el catálogo c_Moneda."));
            return;
        }

        var tipoCambio = solicitud.TipoCambio;
        switch (moneda)
        {
            case "XXX" when tipoCambio is not null:
                errores.Add(new("tipoCambio", "Con la moneda XXX (sin moneda) no se registra tipo de cambio."));
                break;
            case "MXN" when tipoCambio is not null && tipoCambio != 1m:
                errores.Add(new("tipoCambio", "Con moneda MXN el tipo de cambio se omite o vale 1."));
                break;
            case not ("MXN" or "XXX"):
                if (tipoCambio is null)
                {
                    errores.Add(new("tipoCambio", $"Con la moneda {moneda} el tipo de cambio es obligatorio."));
                }
                else if (tipoCambio <= 0m)
                {
                    errores.Add(new("tipoCambio", "El tipo de cambio debe ser mayor que cero."));
                }
                else if (Redondeo.Decimales(tipoCambio.Value) > Redondeo.DecimalesMaximos)
                {
                    errores.Add(new("tipoCambio", "El tipo de cambio admite hasta 6 decimales."));
                }

                break;
        }
    }

    private static void ValidarConcepto(
        List<ErrorCampo> errores, string ruta, SolicitudConcepto concepto, CatalogosPequenos catalogos)
    {
        var cantidadValida = ValidarMonto(errores, $"{ruta}.cantidad", "La cantidad", concepto.Cantidad);
        var valorValido = ValidarMonto(errores, $"{ruta}.valorUnitario", "El valor unitario", concepto.ValorUnitario);

        decimal? importe = null;
        if (cantidadValida && valorValido)
        {
            var producto = concepto.Cantidad * concepto.ValorUnitario;
            if (producto >= LimiteImporte)
            {
                errores.Add(new($"{ruta}.valorUnitario",
                    "El importe del concepto (cantidad por valor unitario) debe ser menor que 1,000,000,000,000,000."));
            }
            else
            {
                importe = Redondeo.Redondear(producto, Redondeo.DecimalesMaximos);
            }
        }

        if (concepto.Descuento is { } descuento)
        {
            if (descuento < 0m)
            {
                errores.Add(new($"{ruta}.descuento", "El descuento no puede ser negativo."));
            }
            else if (Redondeo.Decimales(descuento) > Redondeo.DecimalesMaximos)
            {
                errores.Add(new($"{ruta}.descuento", "El descuento admite hasta 6 decimales."));
            }
            else if (importe is { } tope && descuento > tope)
            {
                errores.Add(new($"{ruta}.descuento",
                    $"El descuento ({Formato.Importe(descuento, 0)}) no puede ser mayor que el importe del concepto ({Formato.Importe(tope, 0)})."));
            }
        }

        var traslados = concepto.Traslados ?? [];
        var retenciones = concepto.Retenciones ?? [];
        var llevaImpuestos = traslados.Count + retenciones.Count > 0;
        var objeto = concepto.ObjetoImp ?? "01";

        if (!catalogos.ObjetosImp.ContainsKey(objeto))
        {
            errores.Add(new($"{ruta}.objetoImp", $"La clave {objeto} no está en el catálogo c_ObjetoImp."));
        }
        else if (objeto == "02" && !llevaImpuestos)
        {
            errores.Add(new($"{ruta}.traslados",
                "Con objeto de impuesto 02 el concepto debe llevar al menos un impuesto trasladado o retenido."));
        }
        else if (objeto is "01" or "03" or "04" or "05" && llevaImpuestos)
        {
            errores.Add(new($"{ruta}.objetoImp",
                $"Con objeto de impuesto {objeto} el concepto no lleva impuestos: quítalos o cambia la clave a 02."));
        }

        for (var i = 0; i < traslados.Count; i++)
        {
            ValidarImpuesto(errores, $"{ruta}.traslados[{i}]", traslados[i], esTraslado: true, catalogos);
        }

        for (var i = 0; i < retenciones.Count; i++)
        {
            ValidarImpuesto(errores, $"{ruta}.retenciones[{i}]", retenciones[i], esTraslado: false, catalogos);
        }

        // Con Tasa y Exento la base es el importe menos el descuento; si el descuento se lo come todo, no hay base.
        var usaBaseMonetaria = traslados.Concat(retenciones).Any(i => i.Base is null && i.TipoFactor != "Cuota");
        if (usaBaseMonetaria && importe is { } importeValido && importeValido - (concepto.Descuento ?? 0m) <= 0m)
        {
            errores.Add(new($"{ruta}.descuento",
                "Con este descuento la base de los impuestos queda en cero: la base (importe menos descuento) debe ser mayor que cero."));
        }
    }

    /// <summary>Cantidad y valor unitario: mayores que cero, de menos de doce cifras enteras y con hasta seis decimales.</summary>
    private static bool ValidarMonto(List<ErrorCampo> errores, string campo, string nombre, decimal valor)
    {
        if (valor <= 0m)
        {
            errores.Add(new(campo, $"{nombre} debe ser mayor que cero."));
        }
        else if (valor >= LimiteCantidad)
        {
            errores.Add(new(campo, $"{nombre} debe ser menor que 1,000,000,000,000."));
        }
        else if (Redondeo.Decimales(valor) > Redondeo.DecimalesMaximos)
        {
            errores.Add(new(campo, $"{nombre} admite hasta 6 decimales."));
        }
        else
        {
            return true;
        }

        return false;
    }

    private static void ValidarImpuesto(
        List<ErrorCampo> errores, string ruta, SolicitudImpuesto impuesto, bool esTraslado, CatalogosPequenos catalogos)
    {
        var accion = esTraslado ? "traslada" : "retiene";
        if (impuesto.Impuesto is null || !catalogos.Impuestos.TryGetValue(impuesto.Impuesto, out var registro))
        {
            errores.Add(new($"{ruta}.impuesto", "Elige el impuesto: 001 (ISR), 002 (IVA) o 003 (IEPS)."));
        }
        else if (esTraslado ? !registro.Traslado : !registro.Retencion)
        {
            errores.Add(new($"{ruta}.impuesto", $"El impuesto {registro.Clave} ({registro.Descripcion}) no se {accion}."));
        }

        switch (impuesto.TipoFactor)
        {
            case "Exento" when !esTraslado:
                errores.Add(new($"{ruta}.tipoFactor", "Una retención no puede ser exenta: elige Tasa o Cuota."));
                break;
            case "Exento":
                if (impuesto.TasaOCuota is not null)
                {
                    errores.Add(new($"{ruta}.tasaOCuota", "Con el factor Exento no se registra tasa ni cuota."));
                }

                break;
            case "Tasa" or "Cuota":
                if (impuesto.TasaOCuota is not { } tasa)
                {
                    errores.Add(new($"{ruta}.tasaOCuota", $"Con el factor {impuesto.TipoFactor} la tasa o cuota es obligatoria."));
                }
                else if (tasa < 0m)
                {
                    errores.Add(new($"{ruta}.tasaOCuota", "La tasa o cuota no puede ser negativa."));
                }
                else if (Redondeo.Decimales(tasa) > Redondeo.DecimalesMaximos)
                {
                    errores.Add(new($"{ruta}.tasaOCuota", "La tasa o cuota admite hasta 6 decimales."));
                }

                break;
            default:
                errores.Add(new($"{ruta}.tipoFactor", "El tipo de factor es Tasa, Cuota o Exento."));
                break;
        }

        if (impuesto.Base is { } baseIndicada)
        {
            if (baseIndicada < 0m)
            {
                errores.Add(new($"{ruta}.base", "La base debe ser mayor que cero."));
            }
            else if (baseIndicada >= LimiteImporte)
            {
                errores.Add(new($"{ruta}.base", "La base debe ser menor que 1,000,000,000,000,000."));
            }
            else if (Redondeo.Decimales(baseIndicada) > Redondeo.DecimalesMaximos)
            {
                errores.Add(new($"{ruta}.base", "La base admite hasta 6 decimales."));
            }
        }
    }
}
