using System.Text.Json.Serialization;

namespace MiTesterE2E.Orchestration.Contracts;

/// <summary>
/// DTO de respuesta para POST /api/v1/orchestration/validate-schema.
/// Mapeado desde el schema swagger.yaml → ValidationResponse.
/// </summary>
public sealed class ValidationResponse
{
    /// <summary>
    /// Indica si el JSON recibido es válido contra el schema-v1.1.json.
    /// </summary>
    [JsonPropertyName("isValid")]
    public bool IsValid { get; init; }

    /// <summary>
    /// Lista de errores de validación en caso de que IsValid = false.
    /// Cada elemento describe en lenguaje natural el campo y la regla infringida.
    /// </summary>
    [JsonPropertyName("validationErrors")]
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];
}
