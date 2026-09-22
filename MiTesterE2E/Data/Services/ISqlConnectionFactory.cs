using System.Data.Common;

namespace MiTesterE2E.Data.Services;

/// <summary>
/// Factoría para resolver dinámicamente conexiones a bases de datos relacionales según el motor y environmentRef.
/// </summary>
public interface ISqlConnectionFactory
{
    /// <summary>
    /// Resuelve la cadena de conexión buscando en ConnectionStrings:{environmentRef}_{catalog} o fallbacks.
    /// </summary>
    string? ResolveConnectionString(string? environmentRef, string catalog);

    /// <summary>
    /// Crea y retorna una instancia de DbConnection para el catálogo especificado.
    /// Retorna null si se debe operar en modo Mock/Simulación.
    /// </summary>
    DbConnection? CreateConnection(string? environmentRef, string catalog);

    /// <summary>
    /// Indica si el sistema está configurado para operar en modo de simulación de datos sintéticos.
    /// </summary>
    bool IsMockDataEnabled { get; }
}
