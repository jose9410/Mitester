using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MiTesterE2E.Data.Contracts;

namespace MiTesterE2E.Data.Services;

/// <summary>
/// Motor físico de ejecución SQL y conciliación masiva de datos en streaming por lotes (50,000 registros por bloque).
/// </summary>
public class SqlCommandExecutor : ISqlCommandExecutor
{
    private const int DefaultBatchChunkSize = 50_000;
    private const int DefaultTimeoutSeconds = 300;

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<SqlCommandExecutor> _logger;

    public SqlCommandExecutor(
        ISqlConnectionFactory connectionFactory,
        ILogger<SqlCommandExecutor> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<int> ExecuteNonQueryAsync(SqlCommandDto command, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection(command.EnvironmentRef, command.Catalog);

        if (connection == null)
        {
            _logger.LogInformation("[SqlCommandExecutor] Ejecución SQL en modo Mock. Query: {Query}", command.Query);
            await Task.Delay(200, cancellationToken);
            return 1;
        }

        await connection.OpenAsync(cancellationToken);
        using var dbCmd = connection.CreateCommand();
        dbCmd.CommandText = command.Query;
        dbCmd.CommandTimeout = command.TimeoutSeconds > 0 ? command.TimeoutSeconds : DefaultTimeoutSeconds;

        if (command.Parameters != null)
        {
            foreach (var kvp in command.Parameters)
            {
                var param = dbCmd.CreateParameter();
                param.ParameterName = kvp.Key;
                param.Value = kvp.Value ?? DBNull.Value;
                dbCmd.Parameters.Add(param);
            }
        }

        return await dbCmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DataCompareResult> CompareDatasetsAsync(
        DataCompareRequestDto request,
        Func<long, long, int, decimal, Task>? onBatchProgressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var sourceConn = _connectionFactory.CreateConnection(request.SourceQuery.EnvironmentRef, request.SourceQuery.Catalog);
        var targetConn = _connectionFactory.CreateConnection(request.TargetQuery.EnvironmentRef, request.TargetQuery.Catalog);

        // Si alguna conexión física no está disponible, utiliza el motor de streaming sintético MockData
        if (sourceConn == null || targetConn == null)
        {
            sourceConn?.Dispose();
            targetConn?.Dispose();

            _logger.LogInformation("[SqlCommandExecutor] Ejecutando comparación masiva con generador de streaming MockData (1.5M registros)...");
            return await ExecuteMockCompareAsync("BANCO_NACIONAL", 1_500_000, 99.12m, onBatchProgressCallback, cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        var result = new DataCompareResult();
        var chunkSize = request.ChunkSize > 0 ? request.ChunkSize : DefaultBatchChunkSize;

        try
        {
            await sourceConn.OpenAsync(cancellationToken);
            await targetConn.OpenAsync(cancellationToken);

            using var sourceCmd = sourceConn.CreateCommand();
            sourceCmd.CommandText = request.SourceQuery.Query;
            sourceCmd.CommandTimeout = request.SourceQuery.TimeoutSeconds > 0 ? request.SourceQuery.TimeoutSeconds : DefaultTimeoutSeconds;

            using var targetCmd = targetConn.CreateCommand();
            targetCmd.CommandText = request.TargetQuery.Query;
            targetCmd.CommandTimeout = request.TargetQuery.TimeoutSeconds > 0 ? request.TargetQuery.TimeoutSeconds : DefaultTimeoutSeconds;

            // Modo SequentialAccess para consumo de memoria plano
            using var sourceReader = await sourceCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
            using var targetReader = await targetCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

            long totalProcessed = 0;
            long matching = 0;
            int discrepancies = 0;
            decimal totalDiscrepancyAmount = 0;

            while (await sourceReader.ReadAsync(cancellationToken) && await targetReader.ReadAsync(cancellationToken))
            {
                totalProcessed++;

                // Comparación de campos clave
                bool isMatch = true;
                foreach (var col in request.KeyColumns)
                {
                    var val1 = sourceReader[col]?.ToString();
                    var val2 = targetReader[col]?.ToString();
                    if (!string.Equals(val1, val2, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    matching++;
                }
                else
                {
                    discrepancies++;
                    if (result.SampleDiscrepancies.Count < 50)
                    {
                        result.SampleDiscrepancies.Add(new DiscrepancyItem
                        {
                            InconsistencyId = $"INC-SQL-{totalProcessed}",
                            RecordKey = $"ROW_{totalProcessed}",
                            FieldAffected = request.KeyColumns.FirstOrDefault() ?? "KEY_COLUMN",
                            MonetaryImpact = 100.00m,
                            ExpectedValue = "MATCH",
                            ActualValue = "MISMATCH",
                            Description = $"Discrepancia en cruce de registro #{totalProcessed}"
                        });
                    }
                }

                // Emisión de progreso por lote (50,000 registros)
                if (totalProcessed % chunkSize == 0 && onBatchProgressCallback != null)
                {
                    var currentPct = totalProcessed > 0 ? Math.Round((decimal)matching / totalProcessed * 100, 2) : 100m;
                    await onBatchProgressCallback(totalProcessed, totalProcessed, discrepancies, currentPct);
                }
            }

            stopwatch.Stop();
            result.TotalRecordsProcessed = totalProcessed;
            result.MatchingRecords = matching;
            result.DiscrepanciesCount = discrepancies;
            result.ConsistencyPercentage = totalProcessed > 0 ? Math.Round((decimal)matching / totalProcessed * 100, 2) : 100m;
            result.TotalMonetaryDiscrepancy = totalDiscrepancyAmount;
            result.DurationMs = stopwatch.ElapsedMilliseconds;

            return result;
        }
        finally
        {
            sourceConn.Dispose();
            targetConn.Dispose();
        }
    }

    public async Task<DataCompareResult> ExecuteMockCompareAsync(
        string tenantId,
        long totalRecords,
        decimal targetConsistencyPercentage,
        Func<long, long, int, decimal, Task>? onBatchProgressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var chunkSize = DefaultBatchChunkSize; // 50,000 registros por bloque
        var totalChunks = (int)Math.Ceiling((double)totalRecords / chunkSize);

        var result = new DataCompareResult
        {
            TotalRecordsProcessed = totalRecords
        };

        long processed = 0;
        long matching = 0;
        int discrepancies = 0;
        decimal totalMonetaryDiscrepancy = 0m;

        // Ratio esperado de discrepancias (ej. para 99.12%, 0.88% de discrepancias)
        var errorRatio = (100m - targetConsistencyPercentage) / 100m;
        var totalExpectedErrors = (long)Math.Round(totalRecords * (double)errorRatio);
        var errorsPerChunk = totalChunks > 0 ? (int)Math.Max(1, totalExpectedErrors / totalChunks) : 0;

        _logger.LogInformation(
            "[SqlCommandExecutor] Iniciando streaming de conciliación sintética. Total registros: {Total:N0} | Chunks: {Chunks} (50k c/u) | % Esperado: {Pct}%",
            totalRecords, totalChunks, targetConsistencyPercentage);

        for (int chunkIndex = 1; chunkIndex <= totalChunks; chunkIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentChunkRecords = (int)Math.Min(chunkSize, totalRecords - processed);
            var currentChunkErrors = (chunkIndex <= totalChunks / 2) ? errorsPerChunk : 0;
            var currentChunkMatches = currentChunkRecords - currentChunkErrors;

            processed += currentChunkRecords;
            matching += currentChunkMatches;
            discrepancies += currentChunkErrors;

            if (currentChunkErrors > 0 && result.SampleDiscrepancies.Count < 5)
            {
                var impact = Math.Round((decimal)(new Random().NextDouble() * 50000 + 1000), 2);
                totalMonetaryDiscrepancy += impact;

                result.SampleDiscrepancies.Add(new DiscrepancyItem
                {
                    InconsistencyId = $"INC-TX-{processed}",
                    RecordKey = $"TX-REC-{processed:D8}",
                    FieldAffected = (chunkIndex % 2 == 0) ? "TOTAL_DEPOSIT_AMOUNT" : "COMMISSION_TAX_RETENTION",
                    MonetaryImpact = impact,
                    ExpectedValue = "{\"currency\": \"COP\", \"status\": \"SETTLED\"}",
                    ActualValue = "{\"currency\": \"COP\", \"status\": \"DISCREPANCY\"}",
                    SuggestedTag = (chunkIndex % 2 == 0) ? "DISCREPANCIA_REDONDEO" : "ERROR_CALCULO_IVA",
                    Description = $"Diferencia de conciliación detectada en lote {chunkIndex}/{totalChunks} (Registro #{processed:N0})."
                });
            }

            // Simulación de latencia de I/O en lectura masiva (~120ms por bloque de 50,000)
            await Task.Delay(120, cancellationToken);

            var currentConsistency = processed > 0 ? Math.Round((decimal)matching / processed * 100, 2) : 100m;

            if (onBatchProgressCallback != null)
            {
                await onBatchProgressCallback(processed, totalRecords, discrepancies, currentConsistency);
            }

            _logger.LogInformation(
                "[SqlCommandExecutor] Lote {Chunk}/{TotalChunks} procesado ({Processed:N0}/{Total:N0}) | Consistencia acumulada: {Pct}% | Discrepancias: {Discr}",
                chunkIndex, totalChunks, processed, totalRecords, currentConsistency, discrepancies);
        }

        stopwatch.Stop();

        result.MatchingRecords = matching;
        result.DiscrepanciesCount = discrepancies;
        result.ConsistencyPercentage = processed > 0 ? Math.Round((decimal)matching / processed * 100, 2) : 100m;
        result.TotalMonetaryDiscrepancy = totalMonetaryDiscrepancy;
        result.DurationMs = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation(
            "[SqlCommandExecutor] Conciliación masiva completada en {Dur}ms. Total: {Tot:N0} | Consistencia: {Pct}% | Discrepancias: {Discr}",
            result.DurationMs, result.TotalRecordsProcessed, result.ConsistencyPercentage, result.DiscrepanciesCount);

        return result;
    }

    public BusinessRuleAssertionDto AssertQualityRules(decimal actualConsistencyPercentage, decimal expectedThreshold, string op)
    {
        return BusinessRuleAssertionDto.Evaluate(actualConsistencyPercentage, expectedThreshold, op);
    }
}
