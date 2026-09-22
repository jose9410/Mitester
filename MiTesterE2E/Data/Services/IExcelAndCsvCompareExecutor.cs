using MiTesterE2E.Data.Contracts;

namespace MiTesterE2E.Data.Services;

/// <summary>
/// Contrato para la comparación masiva de archivos planos (CSV) y libros de cálculo (Excel XLSX/XLS)
/// generados en el directorio /out/ durante la ejecución de suites de prueba.
/// </summary>
public interface IExcelAndCsvCompareExecutor
{
    /// <summary>
    /// Compara dos archivos (CSV o Excel) fila por fila basándose en las columnas clave y tolerancia monetaria.
    /// </summary>
    Task<DataCompareResult> CompareFilesAsync(
        string sourceFilePath,
        string targetFilePath,
        IReadOnlyList<string> keyColumns,
        decimal numericTolerance = 0.00m,
        int chunkSize = 50_000,
        Func<long, long, int, decimal, Task>? onBatchProgressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Localiza automáticamente los artefactos generados en el directorio /out/ asociados a una ejecución
    /// y realiza la conciliación y cálculo del porcentaje de consistencia.
    /// </summary>
    Task<DataCompareResult> CompareArtifactsInOutDirectoryAsync(
        string outDirectory,
        string executionId,
        IReadOnlyList<string> keyColumns,
        decimal numericTolerance = 0.00m,
        int chunkSize = 50_000,
        Func<long, long, int, decimal, Task>? onBatchProgressCallback = null,
        CancellationToken cancellationToken = default);
}
