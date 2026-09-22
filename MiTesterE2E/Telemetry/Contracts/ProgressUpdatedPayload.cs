namespace MiTesterE2E.Telemetry.Contracts;

/// <summary>
/// Payload del evento SignalR "ExecutionProgressUpdated".
/// Se emite en cada tick del bucle de simulación (10 veces por ejecución).
/// El frontend Angular lo usa para actualizar la barra de progreso y el label del paso activo.
/// </summary>
public sealed class ProgressUpdatedPayload
{
    /// <summary>
    /// Identificador de la ejecución. Permite al cliente filtrar eventos multitenant.
    /// </summary>
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Porcentaje de avance actual (0–100).
    /// </summary>
    public int ProgressPercentage { get; init; }

    /// <summary>
    /// Descripción del paso activo en este momento.
    /// Ejemplo: "Procesando lote 3/10 – Cruce_Tx_Breb"
    /// </summary>
    public string ActiveStepDescription { get; init; } = string.Empty;

    /// <summary>
    /// Bandera booleana ligera que indica si existe captura de pantalla disponible en caso de fallo.
    /// (El contenido Base64 se mantiene desacoplado para no saturar el WebSocket).
    /// </summary>
    public bool HasScreenshot { get; init; }

    /// <summary>
    /// Timestamp UTC del evento de progreso.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
