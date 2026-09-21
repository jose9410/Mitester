using System.Text.Json.Serialization;

namespace MiTesterE2E.Orchestration.Contracts;

/// <summary>
/// DTO de respuesta para POST /api/v1/orchestration/executions/start (HTTP 202 Accepted).
/// Mapeado desde el schema swagger.yaml → StartExecutionResponse.
/// </summary>
public sealed class StartExecutionResponse
{
    /// <summary>
    /// Identificador único de la ejecución. El cliente Angular lo usará
    /// para suscribirse al canal correcto en el TelemetryHub de SignalR.
    /// </summary>
    [JsonPropertyName("executionId")]
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Estado inmediato de la ejecución al encolarla.
    /// Valor fijo: "PROCESSING_ASYNC" tal como define el swagger.yaml.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = "PROCESSING_ASYNC";
}
