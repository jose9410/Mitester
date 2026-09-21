using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MiTesterE2E.Orchestration.Contracts;

/// <summary>
/// DTO de entrada para POST /api/v1/orchestration/executions/start.
/// Mapeado desde el schema swagger.yaml → StartExecutionRequest.
/// </summary>
public sealed class StartExecutionRequest
{
    /// <summary>
    /// Contenido JSON de la suite basado en schema-v1.1.
    /// Se envía como string para preservar la estructura polimórfica
    /// sin que el deserializador de ASP.NET Core lo transforme.
    /// </summary>
    [Required(ErrorMessage = "suiteConfigJson es obligatorio.")]
    [JsonPropertyName("suiteConfigJson")]
    public required string SuiteConfigJson { get; init; }
}
