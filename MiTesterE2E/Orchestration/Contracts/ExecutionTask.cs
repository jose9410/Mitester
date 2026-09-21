namespace MiTesterE2E.Orchestration.Contracts;

/// <summary>
/// Representa una tarea encolada en el Channel interno.
/// Se pasa del Controller al BackgroundWorker a través del IExecutionTaskQueue.
/// </summary>
public sealed class ExecutionTask
{
    /// <summary>
    /// Identificador único de la ejecución. Se genera en el Controller y se retorna
    /// al cliente como parte del StartExecutionResponse (HTTP 202).
    /// </summary>
    public Guid ExecutionId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// El contenido JSON de la suite tal como lo envió el cliente Angular.
    /// El BackgroundWorker lo deserializa para simular los pasos.
    /// </summary>
    public required string SuiteConfigJson { get; init; }

    /// <summary>
    /// Timestamp UTC del momento en que la tarea fue encolada.
    /// </summary>
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;
}
