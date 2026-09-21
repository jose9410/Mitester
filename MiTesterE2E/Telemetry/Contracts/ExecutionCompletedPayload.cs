namespace MiTesterE2E.Telemetry.Contracts;

/// <summary>
/// Payload del evento SignalR "ExecutionCompletedToast".
/// Se emite una única vez al finalizar el procesamiento completo de la ejecución.
/// El frontend Angular lo usa para mostrar una notificación Toast con el resultado.
/// </summary>
public sealed class ExecutionCompletedPayload
{
    /// <summary>
    /// Identificador de la ejecución finalizada.
    /// </summary>
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Resultado final de la ejecución.
    /// Valores posibles: "COMPLETED_SUCCESS" | "COMPLETED_WITH_ERRORS" | "FAILED"
    /// </summary>
    public string FinalStatus { get; init; } = "COMPLETED_SUCCESS";

    /// <summary>
    /// Mensaje descriptivo para el Toast en el frontend.
    /// Ejemplo: "Ejecución Cruce_Tx_Breb completada. 1,500,000 transacciones procesadas."
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Duración total de la ejecución en segundos (para métricas de auditoría).
    /// </summary>
    public double DurationSeconds { get; init; }

    /// <summary>
    /// Timestamp UTC de finalización.
    /// </summary>
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
}
