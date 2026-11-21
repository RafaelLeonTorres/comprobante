using Comprobante.Catalogos;
using Comprobante.Core.Validacion;
using Comprobante.UnitTests.Generacion;
using static Comprobante.UnitTests.Validacion.Siembra;

namespace Comprobante.UnitTests.Validacion;

/// <summary>
/// Las reglas de la matriz de errores del SAT, una por una: a un XML correcto se le siembra el error de la regla, se vuelve a
/// firmar y se comprueba que sale el código que corresponde y nada más (salvo lo que ese mismo error rompe de rebote).
/// </summary>
[Collection(ColeccionCatalogos.Nombre)]
public class ReglasSatTests(CatalogosFixture catalogos)
{
    /// <summary>Las reglas que se comprueban en otras pruebas porque no se pueden sembrar con el mismo método (ver <see cref="ValidadorCfdiTests"/> y <see cref="MotorDeReglasTests"/>).</summary>
    private static readonly string[] ComprobadasAparte = ["CFDI40102", "CFDI40106", "CFDI40209", "CFDI40999"];

    private readonly ValidadorCfdi _validador = Servicios.Validador(catalogos.Catalogos);

    public static TheoryData<string> Casos()
    {
        var datos = new TheoryData<string>();
        foreach (var caso in CasosDeReglas.Todos)
        {
            datos.Add(caso.Id);
        }

        return datos;
    }

    public static TheoryData<string> NombresDeBases()
    {
        var datos = new TheoryData<string>();
        foreach (var nombre in Bases.Keys)
        {
            datos.Add(nombre);
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task Cada_regla_se_dispara_con_su_error_sembrado_y_con_nada_mas(string id)
    {
        var caso = CasosDeReglas.Buscar(id);

        var reporte = await _validador.ValidarAsync(Generar(caso.Escenario, caso.Sembrar));

        var resumen = Resumen(reporte);
        Assert.True(reporte.SelloValido, $"El sello del XML sembrado debería ser válido; salió:{Environment.NewLine}{resumen}");
        var propios = reporte.Hallazgos.Where(h => h.Codigo == caso.Codigo).ToList();
        Assert.True(propios.Count > 0, $"No salió {caso.Codigo}; salió:{Environment.NewLine}{resumen}");
        if (caso.Atributo is not null)
        {
            Assert.True(
                propios.Any(h => h.Atributo == caso.Atributo),
                $"{caso.Codigo} no señala el atributo {caso.Atributo}:{Environment.NewLine}{resumen}");
        }

        var permitidos = caso.Ademas.Append(caso.Codigo).ToHashSet();
        Assert.True(
            reporte.Hallazgos.All(h => permitidos.Contains(h.Codigo)),
            $"Para {caso.Codigo} solo debían salir {string.Join(", ", permitidos)}; salió:{Environment.NewLine}{resumen}");
    }

    [Theory]
    [MemberData(nameof(NombresDeBases))]
    public async Task Los_puntos_de_partida_de_las_pruebas_no_tienen_hallazgos(string nombre)
    {
        var (escenario, preparar) = Bases[nombre];

        var reporte = await _validador.ValidarAsync(Generar(escenario, preparar));

        Assert.True(reporte.EsValido, Resumen(reporte));
        Assert.Empty(reporte.Hallazgos);
        Assert.True(reporte.SelloValido);
    }

    [Fact]
    public void Las_129_reglas_de_la_matriz_estan_clasificadas_una_por_una()
    {
        var delSat = MatrizDeErrores.Todos.Select(e => e.Codigo).ToList();
        var clasificadas = ReglasSat.Todas.Select(r => r.Codigo).ToList();

        Assert.Equal(129, delSat.Count);
        Assert.Equal(delSat.Count, delSat.Distinct().Count());
        Assert.Equal(delSat, clasificadas);
    }

    [Fact]
    public void Cada_regla_que_la_app_revisa_tiene_un_caso_sembrado_o_una_prueba_aparte()
    {
        var conCaso = CasosDeReglas.Todos.Select(c => c.Codigo).ToHashSet();
        var sinCaso = ReglasSat.Todas
            .Where(r => r.Estado != EstadoDeRegla.NoRevisada && !ComprobadasAparte.Contains(r.Codigo))
            .Select(r => r.Codigo)
            .Where(codigo => !conCaso.Contains(codigo))
            .ToList();

        Assert.Empty(sinCaso);
    }

    [Fact]
    public void Los_casos_sembrados_son_de_reglas_que_la_app_dice_revisar()
    {
        foreach (var caso in CasosDeReglas.Todos)
        {
            var regla = ReglasSat.Buscar(caso.Codigo);
            Assert.True(regla is not null, $"{caso.Id}: el código no está en la matriz.");
            Assert.NotEqual(EstadoDeRegla.NoRevisada, regla!.Estado);
        }
    }

    [Fact]
    public void Lo_que_no_se_revisa_o_se_revisa_en_parte_dice_por_que()
    {
        foreach (var regla in ReglasSat.Todas.Where(r => r.Estado != EstadoDeRegla.Revisada))
        {
            Assert.False(string.IsNullOrWhiteSpace(regla.Nota), $"{regla.Codigo} ({regla.Estado}) no explica qué falta.");
        }
    }

    [Fact]
    public void Una_regla_cubierta_por_otra_apunta_a_una_regla_que_si_se_revisa()
    {
        foreach (var regla in ReglasSat.Todas.Where(r => r.CubiertaPor is not null))
        {
            var otra = ReglasSat.Buscar(regla.CubiertaPor!);
            Assert.True(otra is not null, $"{regla.Codigo} dice estar cubierta por {regla.CubiertaPor}, que no existe.");
            Assert.NotEqual(EstadoDeRegla.NoRevisada, otra!.Estado);
        }
    }

    [Fact]
    public void El_recuento_de_reglas_es_el_que_documenta_el_proyecto()
    {
        // Si cambia, hay que actualizar el README y el PLAN: son las cifras que se publican.
        Assert.Equal(103, ReglasSat.Todas.Count(r => r.Estado == EstadoDeRegla.Revisada));
        Assert.Equal(5, ReglasSat.Todas.Count(r => r.Estado == EstadoDeRegla.Parcial));
        Assert.Equal(21, ReglasSat.Todas.Count(r => r.Estado == EstadoDeRegla.NoRevisada));
    }
}
