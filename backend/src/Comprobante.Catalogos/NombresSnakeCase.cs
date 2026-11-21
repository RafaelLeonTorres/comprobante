using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Comprobante.Catalogos;

/// <summary>
/// Pone los nombres de PostgreSQL en snake_case: la tabla FormaPago pasa a forma_pago, la columna InicioVigencia
/// a inicio_vigencia. Coinciden con los nombres de los CSV de sat/catalogos.
/// </summary>
internal static class NombresSnakeCase
{
    public static void Aplicar(IMutableModel modelo)
    {
        foreach (var entidad in modelo.GetEntityTypes())
        {
            var tabla = Convertir(entidad.ClrType.Name);
            entidad.SetTableName(tabla);

            foreach (var propiedad in entidad.GetProperties())
            {
                propiedad.SetColumnName(Convertir(propiedad.Name));
            }

            foreach (var clave in entidad.GetKeys())
            {
                clave.SetName($"pk_{tabla}");
            }

            foreach (var indice in entidad.GetIndexes())
            {
                indice.SetDatabaseName($"ix_{tabla}_{string.Join('_', indice.Properties.Select(p => Convertir(p.Name)))}");
            }
        }
    }

    /// <summary>"TasaOCuota" a "tasa_o_cuota", "Sha256" a "sha256".</summary>
    internal static string Convertir(string nombre)
    {
        var resultado = new StringBuilder(nombre.Length + 4);
        for (var i = 0; i < nombre.Length; i++)
        {
            var letra = nombre[i];
            var empiezaPalabra = char.IsUpper(letra)
                && i > 0
                && (char.IsLower(nombre[i - 1]) || (i + 1 < nombre.Length && char.IsLower(nombre[i + 1])));
            if (empiezaPalabra)
            {
                resultado.Append('_');
            }

            resultado.Append(char.ToLowerInvariant(letra));
        }

        return resultado.ToString();
    }
}
