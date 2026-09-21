using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NJsonSchema;
using NJsonSchema.Validation;
using MiTesterE2E.Orchestration.Contracts;
using MiTesterE2E.Orchestration.Services;

namespace MiTesterE2E.Orchestration.Controllers;

/// <summary>
/// Controlador REST para el Engine de Orquestación.
/// 
/// Endpoints expuestos (prefijo configurado en appsettings, ruta base: /api/v1/orchestration):
///   POST /validate-schema       → Valida un JSON contra schema-v1.1.json usando NJsonSchema
///   POST /executions/start      → Encola una tarea asíncrona y retorna HTTP 202 Accepted
/// 
/// El schema-v1.1.json se carga UNA SOLA VEZ al construir el controlador (Scoped)
/// desde los recursos embebidos en el ensamblado para garantizar disponibilidad en el contenedor.
/// </summary>
[ApiController]
[Route("api/v1/orchestration")]
[Produces("application/json")]
public sealed class OrchestrationController : ControllerBase
{
    // Nombre del recurso embebido. El SDK convierte los separadores de ruta
    // en puntos, por eso el formato es: {Namespace}.{Filename}
    private const string EmbeddedSchemaResourceName = "MiTesterE2E.schema-v1.1.json";

    private readonly IExecutionTaskQueue _taskQueue;
    private readonly ILogger<OrchestrationController> _logger;

    // El JsonSchema se carga como campo estático para parsear NJsonSchema solo una vez
    // en toda la vida del proceso (es una operación costosa con allOf/if-then).
    private static readonly Lazy<Task<JsonSchema>> _lazySchema = new(LoadEmbeddedSchemaAsync);

    public OrchestrationController(
        IExecutionTaskQueue taskQueue,
        ILogger<OrchestrationController> logger)
    {
        _taskQueue = taskQueue;
        _logger    = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/v1/orchestration/validate-schema
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Valida un archivo de suite JSON contra el schema-v1.1.json.
    /// El frontend Angular llama a este endpoint antes de guardar o iniciar una suite.
    /// </summary>
    /// <param name="suiteJson">Objeto JSON libre que representa la suite E2E a validar.</param>
    /// <returns>ValidationResponse con isValid y lista de errores descriptivos.</returns>
    [HttpPost("validate-schema")]
    [ProducesResponseType(typeof(ValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ValidateSchema([FromBody] object suiteJson)
    {
        _logger.LogInformation("[OrchestrationController] Solicitud de validación de schema recibida.");

        try
        {
            var schema = await _lazySchema.Value;

            // Convertimos el objeto desserializado de vuelta a JSON string para NJsonSchema
            var jsonString = System.Text.Json.JsonSerializer.Serialize(suiteJson);

            var errors = schema.Validate(jsonString);

            if (errors.Count == 0)
            {
                _logger.LogInformation(
                    "[OrchestrationController] Validación exitosa — Schema válido.");

                return Ok(new ValidationResponse
                {
                    IsValid          = true,
                    ValidationErrors = []
                });
            }

            var errorMessages = FormatValidationErrors(errors);

            _logger.LogWarning(
                "[OrchestrationController] Validación fallida — {Count} error(es) encontrado(s).",
                errorMessages.Count);

            return Ok(new ValidationResponse
            {
                IsValid          = false,
                ValidationErrors = errorMessages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[OrchestrationController] Error interno durante la validación del schema.");

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                errorCode = "ERR_SCHEMA_VALIDATION_ENGINE",
                message   = "Error interno al ejecutar la validación del schema."
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/v1/orchestration/executions/start
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inicia la ejecución asíncrona de una suite E2E.
    /// 
    /// Flujo:
    ///   1. Valida el suiteConfigJson contra schema-v1.1.json.
    ///   2. Si es inválido, retorna HTTP 400 con los errores de validación.
    ///   3. Si es válido, crea un ExecutionTask con un Guid único y lo encola en el Channel.
    ///   4. Retorna inmediatamente HTTP 202 Accepted con el executionId para que el
    ///      frontend Angular lo use para suscribirse al TelemetryHub de SignalR.
    /// </summary>
    [HttpPost("executions/start")]
    [ProducesResponseType(typeof(StartExecutionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationResponse),    StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> StartExecution(
        [FromBody] StartExecutionRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[OrchestrationController] Solicitud de inicio de ejecución recibida. " +
            "SuiteJson length: {Len} chars",
            request.SuiteConfigJson.Length);

        // ── PASO 1: Validar el JSON contra el schema ──────────────────────────
        JsonSchema schema;
        try
        {
            schema = await _lazySchema.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[OrchestrationController] Error al cargar el schema embebido.");

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                errorCode = "ERR_SCHEMA_LOAD",
                message   = "No se pudo cargar el schema de validación."
            });
        }

        ICollection<ValidationError> validationErrors;
        try
        {
            validationErrors = schema.Validate(request.SuiteConfigJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[OrchestrationController] Error durante la validación del JSON de entrada.");

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                errorCode = "ERR_ORCHESTRATION_ENGINE",
                message   = "Fallo durante la validación del JSON de la suite."
            });
        }

        if (validationErrors.Count > 0)
        {
            var errorMessages = FormatValidationErrors(validationErrors);

            _logger.LogWarning(
                "[OrchestrationController] Ejecución rechazada — {Count} error(es) de schema.",
                errorMessages.Count);

            return BadRequest(new ValidationResponse
            {
                IsValid          = false,
                ValidationErrors = errorMessages
            });
        }

        // ── PASO 2: Encolar la tarea en el Channel ────────────────────────────
        var executionTask = new ExecutionTask
        {
            SuiteConfigJson = request.SuiteConfigJson
        };

        try
        {
            await _taskQueue.EnqueueAsync(executionTask, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[OrchestrationController] Error al encolar la tarea. ExecutionId: {Id}",
                executionTask.ExecutionId);

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                errorCode = "ERR_ORCHESTRATION_ENGINE",
                message   = "Error al encolar la tarea de ejecución."
            });
        }

        _logger.LogInformation(
            "[OrchestrationController] Ejecución aceptada. ExecutionId: {Id} | " +
            "HTTP 202 retornado al cliente.",
            executionTask.ExecutionId);

        // ── PASO 3: Retornar HTTP 202 Accepted con el executionId ─────────────
        return Accepted(new StartExecutionResponse
        {
            ExecutionId = executionTask.ExecutionId,
            Status      = "PROCESSING_ASYNC"
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MÉTODOS PRIVADOS DE SOPORTE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Carga el schema-v1.1.json desde los recursos embebidos del ensamblado.
    /// Se ejecuta una única vez gracias al campo Lazy estático.
    /// </summary>
    private static async Task<JsonSchema> LoadEmbeddedSchemaAsync()
    {
        var assembly  = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(EmbeddedSchemaResourceName)
            ?? throw new InvalidOperationException(
                $"No se encontró el recurso embebido '{EmbeddedSchemaResourceName}'. " +
                "Verifica que el archivo schema-v1.1.json tiene <EmbeddedResource> en el .csproj.");

        using var reader    = new StreamReader(stream);
        var schemaJson = await reader.ReadToEndAsync();

        // NJsonSchema.FromJsonAsync parsea el schema completo incluyendo $ref, allOf, if-then
        return await JsonSchema.FromJsonAsync(schemaJson);
    }

    /// <summary>
    /// Convierte la colección de ValidationError de NJsonSchema en mensajes
    /// legibles en español para el frontend Angular.
    /// </summary>
    private static List<string> FormatValidationErrors(ICollection<ValidationError> errors)
    {
        var messages = new List<string>(errors.Count);

        foreach (var error in errors)
        {
            // NJsonSchema puede tener errores anidados (ChildErrors en allOf/if-then)
            if (error is ChildSchemaValidationError childError && childError.Errors?.Count > 0)
            {
                foreach (var (_, nestedErrors) in childError.Errors)
                {
                    foreach (var nested in nestedErrors)
                    {
                        messages.Add(FormatSingleError(nested));
                    }
                }
            }
            else
            {
                messages.Add(FormatSingleError(error));
            }
        }

        return messages;
    }

    private static string FormatSingleError(ValidationError error)
    {
        var path = string.IsNullOrWhiteSpace(error.Path) ? "raíz" : error.Path;
        return $"[{error.Kind}] En '{path}': {error.ToString()}";
    }
}
