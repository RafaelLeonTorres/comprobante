using System.Text.RegularExpressions;
using System.Xml.Linq;
using Comprobante.Core.Catalogos;
using Comprobante.Core.Validacion;
using Comprobante.Core.Validacion.Reglas;
using Comprobante.UnitTests.Generacion;
using static Comprobante.UnitTests.Validacion.Siembra;

namespace Comprobante.UnitTests.Validacion;

/// <summary>El motor que corre las reglas (red de seguridad, tope de hallazgos) y los avisos que no son de la matriz del SAT.</summary>
[Collection(ColeccionCatalogos.Nombre)]
public partial class MotorDeReglasTests(CatalogosFixture catalogos)
{
    private readonly ValidadorCfdi _validador = Servicios.Validador(catalogos.Catalogos);

    [GeneratedRegex(@"Hay (\d+) hallazgos más", RegexOptions.CultureInvariant)]
    private static partial Regex Omitidos();

    private ContextoValidacion Contexto() =>
        new(
            Cargar("ingreso-basico"),
            catalogos.Pequenos,
            new Dictionary<string, ClaveProdServ>(),
            new Dictionary<string, ClaveUnidad>(),
            new Dictionary<string, CodigoPostal>(),
            Servicios.Ahora);

    [Fact]
    public void Si_un_grupo_de_reglas_falla_se_informa_CFDI40999_y_los_demas_grupos_se_revisan()
    {
        (string Nombre, Action<ContextoValidacion> Revisar)[] grupos =
        [
            ("el primero", _ => throw new InvalidOperationException("detalle interno")),
            ("el segundo", c => c.Error("CFDI40101", c.Raiz, "Fecha", "sí se revisó")),
        ];

        var hallazgos = MotorDeReglas.Revisar(Contexto(), grupos);

        Assert.Equal(["CFDI40999", "CFDI40101"], hallazgos.Select(h => h.Codigo));
        Assert.All(hallazgos, h => Assert.Equal(Severidad.Error, h.Severidad));
        Assert.Contains("el primero", hallazgos[0].Mensaje, StringComparison.Ordinal);
        Assert.DoesNotContain("detalle interno", hallazgos[0].Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_cancelacion_no_se_confunde_con_un_error_del_validador()
    {
        (string Nombre, Action<ContextoValidacion> Revisar)[] grupos =
        [
            ("el primero", _ => throw new OperationCanceledException()),
        ];

        Assert.Throws<OperationCanceledException>(() => MotorDeReglas.Revisar(Contexto(), grupos));
    }

    [Fact]
    public async Task Con_demasiados_hallazgos_se_guardan_300_y_se_cuenta_el_resto()
    {
        // 400 conceptos con una clave de unidad que no existe: 400 hallazgos CFDI40165 más los del subtotal y los impuestos.
        var xml = Generar("ingreso-basico", d =>
        {
            var concepto = d.Nodo(Concepto);
            concepto.SetAttributeValue("ClaveUnidad", "ZZZ");
            for (var i = 0; i < 399; i++)
            {
                concepto.AddAfterSelf(new XElement(concepto));
            }
        });

        var reporte = await _validador.ValidarAsync(xml);

        Assert.Equal(300, reporte.Hallazgos.Count(h => h.Codigo.StartsWith("CFDI", StringComparison.Ordinal)));
        var aviso = Assert.Single(reporte.Hallazgos, h => h.Codigo == Codigos.ReglasOmitidas);
        Assert.Equal(Severidad.Error, aviso.Severidad);
        var omitidos = int.Parse(Omitidos().Match(aviso.Mensaje).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(omitidos >= 100, aviso.Mensaje);
        Assert.False(reporte.EsValido);
    }

    [Theory]
    [InlineData("2026-11-21T09:30:00", "42501", false)] // la hora exacta del centro del país
    [InlineData("2026-11-21T09:31:00", "42501", false)] // un minuto de margen entre relojes
    [InlineData("2026-11-21T09:31:01", "42501", true)]
    [InlineData("2026-11-22T09:30:00", "42501", true)]
    [InlineData("2026-11-21T09:00:00", "82525", true)] // en el Pacífico son las 08:30
    [InlineData("2026-11-21T09:00:00", "42501", false)] // la misma fecha en el centro ya pasó
    public async Task Una_fecha_que_todavia_no_llega_en_el_lugar_de_expedicion_es_una_advertencia(
        string fecha, string codigoPostal, bool avisa)
    {
        var xml = Generar("ingreso-basico", d =>
        {
            d.Poner("", "Fecha", fecha);
            d.Poner("", "LugarExpedicion", codigoPostal);
        });

        var reporte = await _validador.ValidarAsync(xml);

        var aviso = reporte.Hallazgos.SingleOrDefault(h => h.Codigo == Codigos.FechaEnElFuturo);
        Assert.Equal(avisa, aviso is not null);
        Assert.True(reporte.EsValido, Resumen(reporte));
        if (aviso is not null)
        {
            Assert.Equal(Severidad.Advertencia, aviso.Severidad);
            Assert.Equal("Fecha", aviso.Atributo);
            Assert.Equal(fecha, aviso.Encontrado);
            Assert.Equal(codigoPostal == "82525" ? "2026-11-21T08:30:00" : "2026-11-21T09:30:00", aviso.Esperado);
        }
    }

    [Fact]
    public async Task Cada_complemento_se_avisa_como_no_revisado_y_el_timbre_con_su_propio_mensaje()
    {
        XNamespace timbre = "http://www.sat.gob.mx/TimbreFiscalDigital";
        XNamespace otro = "http://example.com/complemento";
        var xml = Generar("ingreso-basico", d =>
        {
            d.Root!.Add(new XElement(
                Cfdi + "Complemento",
                new XElement(
                    timbre + "TimbreFiscalDigital",
                    new XAttribute(XNamespace.Xmlns + "tfd", timbre.NamespaceName),
                    new XAttribute("Version", "1.1"),
                    new XAttribute("UUID", "5FB2822E-396D-4725-8521-CDC4BDD20CCF"),
                    new XAttribute("FechaTimbrado", "2026-11-21T09:31:00"),
                    new XAttribute("RfcProvCertif", "SAT970701NN3"),
                    new XAttribute("SelloCFD", Convert.ToBase64String(new byte[256])),
                    new XAttribute("NoCertificadoSAT", "30001000000500003456"),
                    new XAttribute("SelloSAT", Convert.ToBase64String(new byte[256]))),
                new XElement(otro + "Extra", new XAttribute(XNamespace.Xmlns + "x", otro.NamespaceName))));
            d.Nodo(Concepto).Add(new XElement(
                Cfdi + "ComplementoConcepto",
                new XElement(otro + "DeConcepto", new XAttribute(XNamespace.Xmlns + "x", otro.NamespaceName))));
        });

        var reporte = await _validador.ValidarAsync(xml);

        // Que la app no tenga el esquema de un complemento no es un error del comprobante: solo se avisa.
        Assert.True(reporte.EsValido, Resumen(reporte));
        Assert.DoesNotContain(reporte.Hallazgos, h => h.Codigo == Codigos.Esquema);
        var avisos = reporte.Hallazgos.Where(h => h.Codigo == Codigos.ComplementoNoRevisado).ToList();
        Assert.Equal(3, avisos.Count);
        Assert.All(avisos, a => Assert.Equal(Severidad.Advertencia, a.Severidad));
        Assert.Contains(avisos, a => a.Mensaje.Contains("Timbre Fiscal Digital", StringComparison.Ordinal));
        Assert.Contains(avisos, a => a.Mensaje.Contains("Extra", StringComparison.Ordinal));
        Assert.Contains(avisos, a => a.Mensaje.Contains("DeConcepto", StringComparison.Ordinal));
        Assert.Contains(avisos, a => a.Nodo.Contains("ComplementoConcepto", StringComparison.Ordinal));
    }

    [Fact]
    public async Task La_addenda_admite_cualquier_contenido_sin_errores_de_esquema()
    {
        XNamespace propio = "http://example.com/addenda";
        var xml = Generar("ingreso-basico", d => d.Root!.Add(new XElement(
            Cfdi + "Addenda",
            new XElement(propio + "Pedido", new XAttribute(XNamespace.Xmlns + "p", propio.NamespaceName), new XAttribute("Numero", "A-1")))));

        var reporte = await _validador.ValidarAsync(xml);

        Assert.True(reporte.EsValido, Resumen(reporte));
        Assert.Empty(reporte.Hallazgos);
    }

    [Fact]
    public async Task Un_hallazgo_trae_la_linea_del_atributo_que_falla()
    {
        var reporte = await _validador.ValidarAsync(Generar("ingreso-basico", d => d.Poner("Receptor", "UsoCFDI", "Z99")));

        var hallazgo = Assert.Single(reporte.Hallazgos, h => h.Codigo == "CFDI40160");
        Assert.True(hallazgo.Linea > 0, "El hallazgo no trae la línea del XML.");
    }
}
