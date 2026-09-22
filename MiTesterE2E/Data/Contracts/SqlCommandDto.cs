using System.Text.Json.Serialization;

namespace MiTesterE2E.Data.Contracts;

/// <summary>
/// DTO que representa un comando de ejecución SQL contra bases de datos bancarias (Oracle o SQL Server).
/// </summary>
public class SqlCommandDto
{
    /// <summary>
    /// Catálogo / Motor de base de datos destino: "Oracle" | "SQL Server".
    /// </summary>
    [JsonPropertyName("catalog")]
    public string Catalog { get; set; } = "SQL Server";

    /// <summary>
    /// Sentencia o consulta SQL a ejecutar.
    /// </summary>
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Optimización para Oracle: Forzar escaneo rápido de índice.
    /// </summary>
    [JsonPropertyName("useIndexFastFullScan")]
    public bool UseIndexFastFullScan { get; set; }

    /// <summary>
    /// Timeout máximo en segundos para la consulta (por defecto 300 segundos / 5 minutos).
    /// </summary>
    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Referencia segura al gestor de secretos / Key Vault (ej. "kv-bpp-ktx-qa").
    /// </summary>
    [JsonPropertyName("environmentRef")]
    public string? EnvironmentRef { get; set; }

    /// <summary>
    /// Parámetros opcionales de la consulta SQL.
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }
}
