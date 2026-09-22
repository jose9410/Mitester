using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MiTesterE2E.Data.Services;

/// <summary>
/// Implementación de ISqlConnectionFactory con resolución dinámica de cadenas de conexión por environmentRef y catálogo.
/// </summary>
public class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SqlConnectionFactory> _logger;

    public SqlConnectionFactory(IConfiguration configuration, ILogger<SqlConnectionFactory> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsMockDataEnabled => _configuration.GetValue<bool>("Data:UseMockSqlData", true);

    public string? ResolveConnectionString(string? environmentRef, string catalog)
    {
        var normalizedCatalog = catalog?.Trim().Replace(" ", "") ?? "SqlServer";

        // 1. Prioridad: ConnectionStrings:{environmentRef}_{Engine}
        if (!string.IsNullOrWhiteSpace(environmentRef))
        {
            var keyWithRef = $"ConnectionStrings:{environmentRef}_{normalizedCatalog}";
            var connStr = _configuration[keyWithRef];
            if (!string.IsNullOrWhiteSpace(connStr))
            {
                _logger.LogInformation("[SqlConnectionFactory] Cadena resuelta desde clave: {Key}", keyWithRef);
                return connStr;
            }

            // Clave directa con environmentRef
            var directKey = $"ConnectionStrings:{environmentRef}";
            connStr = _configuration[directKey];
            if (!string.IsNullOrWhiteSpace(connStr))
            {
                return connStr;
            }
        }

        // 2. Fallbacks estándar por motor (SqlServer / Oracle / AzureSql)
        var fallbackKey = $"ConnectionStrings:{normalizedCatalog}";
        var fallbackConn = _configuration[fallbackKey];
        if (!string.IsNullOrWhiteSpace(fallbackConn))
        {
            return fallbackConn;
        }

        // 3. Fallback genérico AzureSql
        return _configuration["ConnectionStrings:AzureSql"];
    }

    public DbConnection? CreateConnection(string? environmentRef, string catalog)
    {
        if (IsMockDataEnabled)
        {
            _logger.LogInformation("[SqlConnectionFactory] Modo simulación MockData activo. Omitiendo apertura física de conexión.");
            return null;
        }

        var connString = ResolveConnectionString(environmentRef, catalog);
        if (string.IsNullOrWhiteSpace(connString))
        {
            _logger.LogWarning("[SqlConnectionFactory] No se encontró cadena de conexión para {Env}_{Catalog}. Conmutando a modo Mock...", environmentRef, catalog);
            return null;
        }

        var normalizedCatalog = catalog?.Trim().ToUpperInvariant() ?? "SQL SERVER";

        if (normalizedCatalog.Contains("SQL SERVER") || normalizedCatalog.Contains("SQLSERVER") || normalizedCatalog.Contains("AZURE"))
        {
            return new SqlConnection(connString);
        }

        // Para Oracle en modo sin driver directo instalado, opera con SqlConnection o fallback seguro
        return new SqlConnection(connString);
    }
}
