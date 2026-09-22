using System.Text.Json.Serialization;

namespace MiTesterE2E.Data.Contracts;

/// <summary>
/// DTO que define una operación de cruce masivo (DATA_COMPARE) entre dos fuentes de datos.
/// </summary>
public class DataCompareRequestDto
{
    [JsonPropertyName("sourceQuery")]
    public SqlCommandDto SourceQuery { get; set; } = new();

    [JsonPropertyName("targetQuery")]
    public SqlCommandDto TargetQuery { get; set; } = new();

    /// <summary>
    /// Claves primarias o de correlación para el cruce de transacciones (ej. ["TRANSACTION_ID", "ACCOUNT_NUM"]).
    /// </summary>
    [JsonPropertyName("keyColumns")]
    public List<string> KeyColumns { get; set; } = new();

    /// <summary>
    /// Columnas numéricas/monetarias sujetas a validación de tolerancia (ej. ["AMOUNT", "TAX_AMOUNT"]).
    /// </summary>
    [JsonPropertyName("monetaryColumns")]
    public List<string> MonetaryColumns { get; set; } = new();

    /// <summary>
    /// Tolerancia permitida en diferencias monetarias (ej. 0.00 o 0.01).
    /// </summary>
    [JsonPropertyName("tolerance")]
    public decimal Tolerance { get; set; } = 0.00m;

    /// <summary>
    /// Tamaño de lote para streaming y emisión de telemetría (por defecto 50,000 registros).
    /// </summary>
    [JsonPropertyName("chunkSize")]
    public int ChunkSize { get; set; } = 50_000;
}
