using Comprobante.Core.Generacion;

namespace Comprobante.UnitTests.Generacion;

/// <summary>
/// Comprobante contra una implementación independiente: el mismo comprobante armado con Python, lxml (libxslt para la cadena
/// original, libxml2 para el XSD) y OpenSSL (el sello). Como el sello PKCS#1 v1.5 es determinista, todo el XML debe coincidir.
/// </summary>
[Collection(ColeccionCatalogos.Nombre)]
public class ReferenciaTests(CatalogosFixture catalogos)
{
    public static TheoryData<string> Escenarios() => Referencia.Escenarios();

    [Fact]
    public void Hay_escenarios_de_referencia() => Assert.True(Referencia.Nombres.Count >= 5);

    [Theory]
    [MemberData(nameof(Escenarios))]
    public async Task El_XML_generado_es_identico_al_de_la_implementacion_independiente(string escenario)
    {
        var resultado = await Servicios.Generacion(catalogos.Catalogos).GenerarAsync(Referencia.Solicitud(escenario));

        Assert.True(resultado.EsCorrecto, string.Join("; ", resultado.Errores.Select(e => $"{e.Campo}: {e.Mensaje}")));
        Assert.Equal(Referencia.Xml(escenario), resultado.Valor.Xml);
        Assert.Equal(Referencia.Cadena(escenario), resultado.Valor.CadenaOriginal);
    }

    [Theory]
    [MemberData(nameof(Escenarios))]
    public async Task El_XML_generado_cumple_el_esquema_y_su_sello_verifica(string escenario)
    {
        var resultado = await Servicios.Generacion(catalogos.Catalogos).GenerarAsync(Referencia.Solicitud(escenario));

        var validacion = resultado.Valor!.Validacion;
        Assert.True(validacion.EsValido, string.Join("; ", validacion.Hallazgos.Select(h => $"{h.Codigo} {h.Nodo} {h.Mensaje}")));
        Assert.Empty(validacion.Hallazgos);
        Assert.True(validacion.SelloValido);
        Assert.Equal(resultado.Valor.NoCertificado, validacion.Certificado!.NoCertificado);
        Assert.Equal(resultado.Valor.CadenaOriginal, validacion.CadenaOriginal);
    }

    [Theory]
    [MemberData(nameof(Escenarios))]
    public async Task El_validador_acepta_el_XML_que_firmo_la_implementacion_independiente(string escenario)
    {
        // Al revés: lo que firmó OpenSSL lo verifica el validador de C#.
        var reporte = await Servicios.Validador(catalogos.Catalogos).ValidarAsync(Referencia.Xml(escenario));

        Assert.True(reporte.EsValido, string.Join("; ", reporte.Hallazgos.Select(h => $"{h.Codigo} {h.Nodo} {h.Mensaje}")));
        Assert.True(reporte.SelloValido);
        Assert.Equal(Referencia.Cadena(escenario), reporte.CadenaOriginal);
    }
}
