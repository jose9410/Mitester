namespace MiTesterE2E.Automation.Services;

/// <summary>
/// Contrato para el servicio de extracción e inspección automática de trazas de logs
/// en el directorio /out/ tras la ejecución de pruebas o detección de inconsistencias.
/// </summary>
public interface IFileLogExtractorService
{
    /// <summary>
    /// Busca y extrae el bloque contextual (últimas 25–30 líneas) del log más relevante para la ejecución.
    /// Aplica estrategia híbrida: búsqueda por ExecutionId y fallback a archivo más reciente.
    /// </summary>
    Task<string> ExtractContextualLogAsync(
        string executionId,
        string? applicationName = null,
        DateTimeOffset? executionStartTime = null,
        string? customOutDirectory = null,
        CancellationToken cancellationToken = default);
}
