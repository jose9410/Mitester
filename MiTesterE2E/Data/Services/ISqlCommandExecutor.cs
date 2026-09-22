using MiTesterE2E.Data.Contracts;

namespace MiTesterE2E.Data.Services;

/// <summary>
/// Contrato para ejecución de consultas SQL y comparación masiva de datos en streaming (hasta 1.5M registros).
/// </summary>
public interface ISqlCommandExecutor
{
    /// <summary>
    /// Ejecuta una consulta SQL de extracción o mutación contra la base de datos configurada.
    /// </summary>
    Task<int> ExecuteNonQueryAsync(SqlCommandDto command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Realiza una comparación y conciliación masiva entre dos consultas SQL en streaming continuo por bloques (50,000 registros).
    /// </summary>
    Task<DataCompareResult> CompareDatasetsAsync(
        DataCompareRequestDto request,
        Func<long, long, int, decimal, Task>? onBatchProgressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Genera y procesa en memoria un flujo de 1,500,000 registros sintéticos en lotes de 50,000 para pruebas de conciliación.
    /// </summary>
    Task<DataCompareResult> ExecuteMockCompareAsync(
        string tenantId,
        long totalRecords,
        decimal targetConsistencyPercentage,
        Func<long, long, int, decimal, Task>? onBatchProgressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evalúa las reglas de aserción de negocio (ASSERT_BUSINESS_RULES) sobre el % de consistencia.
    /// </summary>
    BusinessRuleAssertionDto AssertQualityRules(decimal actualConsistencyPercentage, decimal expectedThreshold, string op);
}
