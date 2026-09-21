using Microsoft.AspNetCore.SignalR;

namespace MiTesterE2E.Telemetry;

/// <summary>
/// Hub de SignalR para telemetría en tiempo real.
/// 
/// El frontend Angular se conectará a: /hubs/telemetry
/// 
/// Eventos que emite el servidor → cliente:
///   - "ExecutionProgressUpdated"  : ProgressUpdatedPayload   (10 veces por ejecución, cada ~500ms)
///   - "ExecutionCompletedToast"   : ExecutionCompletedPayload (1 vez al finalizar)
/// 
/// El Hub en sí mismo es stateless (sin métodos que el cliente llame al servidor en Fase 1).
/// Toda la lógica de emisión es iniciada por ExecutionBackgroundWorker via IHubContext&lt;TelemetryHub&gt;.
/// </summary>
public sealed class TelemetryHub : Hub
{
    private readonly ILogger<TelemetryHub> _logger;

    public TelemetryHub(ILogger<TelemetryHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Invocado automáticamente por SignalR cuando un cliente establece conexión.
    /// Registra el evento para auditoría.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "[TelemetryHub] Cliente conectado. ConnectionId: {ConnectionId} | Timestamp: {Ts}",
            Context.ConnectionId,
            DateTimeOffset.UtcNow);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Invocado automáticamente por SignalR cuando un cliente se desconecta.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogWarning(
                "[TelemetryHub] Cliente desconectado con error. ConnectionId: {ConnectionId} | Error: {Msg}",
                Context.ConnectionId,
                exception.Message);
        }
        else
        {
            _logger.LogInformation(
                "[TelemetryHub] Cliente desconectado normalmente. ConnectionId: {ConnectionId}",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
