using System.Text.Json;
using Comprobante.Core.Validacion;
using Comprobante.Core.Xml;

namespace Comprobante.Core.Sello;

/// <summary>Un emisor de los certificados de prueba del SAT, con los datos que el SAT registra para su RFC.</summary>
/// <param name="Persona">"moral" o "fisica".</param>
/// <param name="Archivo">Nombre de sus archivos .cer y .key.</param>
public sealed record EmisorDePrueba(
    string Rfc, string Nombre, string Persona, string CodigoPostal, string RegimenFiscal, string Archivo);

/// <summary>
/// Los diez CSD de prueba que publica el SAT (públicos; su contraseña también). Son los únicos con los que la app firma:
/// una llave privada real no debe llegar a un servidor.
/// </summary>
public sealed class EmisoresDePrueba : IDisposable
{
    private readonly Dictionary<string, Lazy<Csd>> _csd;
    private readonly Dictionary<string, EmisorDePrueba> _porRfc;

    public EmisoresDePrueba()
    {
        using var flujo = RecursosSat.Abrir("sat/csd-prueba/emisores.json");
        using var documento = JsonDocument.Parse(flujo);
        var contrasena = documento.RootElement.GetProperty("contrasena").GetString()!;
        var emisores = documento.RootElement.GetProperty("emisores").EnumerateArray()
            .Select(e => new EmisorDePrueba(
                e.GetProperty("rfc").GetString()!,
                e.GetProperty("nombre").GetString()!,
                e.GetProperty("persona").GetString()!,
                e.GetProperty("codigoPostal").GetString()!,
                e.GetProperty("regimenFiscal").GetString()!,
                e.GetProperty("archivo").GetString()!))
            .ToList();

        Todos = emisores;
        _porRfc = emisores.ToDictionary(e => e.Rfc, StringComparer.OrdinalIgnoreCase);
        _csd = emisores.ToDictionary(
            e => e.Rfc,
            e => new Lazy<Csd>(
                () => Csd.Cargar(
                    RecursosSat.Bytes($"sat/csd-prueba/{e.Archivo}.cer"),
                    RecursosSat.Bytes($"sat/csd-prueba/{e.Archivo}.key"),
                    contrasena),
                LazyThreadSafetyMode.ExecutionAndPublication),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<EmisorDePrueba> Todos { get; }

    public EmisorDePrueba? Buscar(string? rfc) =>
        rfc is not null && _porRfc.TryGetValue(rfc, out var emisor) ? emisor : null;

    /// <summary>El CSD de un emisor de prueba (se lee la primera vez que se pide); nulo si el RFC no es de los de prueba.</summary>
    public Csd? ObtenerCsd(string? rfc) =>
        rfc is not null && _csd.TryGetValue(rfc, out var csd) ? csd.Value : null;

    /// <summary>Los datos del certificado de un emisor de prueba (NoCertificado y vigencia).</summary>
    public DatosCertificado? Certificado(string? rfc) => ObtenerCsd(rfc)?.Datos;

    public void Dispose()
    {
        foreach (var csd in _csd.Values.Where(c => c.IsValueCreated))
        {
            csd.Value.Dispose();
        }
    }
}
