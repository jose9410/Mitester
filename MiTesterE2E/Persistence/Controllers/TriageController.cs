using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiTesterE2E.Persistence.Context;
using MiTesterE2E.Persistence.Multitenancy;

namespace MiTesterE2E.Persistence.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class TriageController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly ILogger<TriageController> _logger;

    public TriageController(
        AppDbContext context,
        ITenantService tenantService,
        ILogger<TriageController> logger)
    {
        _context = context;
        _tenantService = tenantService;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint de Carga Perezosa (Lazy Loading) para el Drawer UI de Triage.
    /// Retorna los detalles completos de la inconsistencia, trazas de logs y la captura de pantalla en Base64.
    /// </summary>
    /// <param name="inconsistencyId">Identificador de la inconsistencia.</param>
    [HttpGet("logs/{inconsistencyId}")]
    public async Task<IActionResult> GetInconsistencyTriageDetail(string inconsistencyId)
    {
        _logger.LogInformation(
            "[TriageController] Consultando detalle de inconsistencia {Id} para Tenant: {Tenant}",
            inconsistencyId, _tenantService.CurrentTenantId);

        // El Global Query Filter garantiza que solo se consulten inconsistencias del Tenant actual
        var inc = await _context.Inconsistencies
                               .Include(i => i.Execution)
                               .FirstOrDefaultAsync(i => i.InconsistencyId == inconsistencyId);

        if (inc == null)
        {
            return NotFound(new
            {
                message = $"No se encontró la inconsistencia '{inconsistencyId}' para el tenant activo '{_tenantService.CurrentTenantId}'."
            });
        }

        // Estructuración del log contextual para el visor del Drawer
        var contextualTrace = new[]
        {
            new { timestamp = inc.DetectedAt.ToString("o"), level = "INFO", message = $"Iniciando análisis contextual de inconsistencia: {inc.InconsistencyId}" },
            new { timestamp = inc.DetectedAt.AddMilliseconds(120).ToString("o"), level = "WARN", message = $"Componente afectado: {inc.Component} | Campo: {inc.FieldAffected}" },
            new { timestamp = inc.DetectedAt.AddMilliseconds(250).ToString("o"), level = "ERROR", message = $"Discrepancia detectada: {inc.Description ?? "Fallo en aserción o comando de interfaz"}" },
            new { timestamp = inc.DetectedAt.AddMilliseconds(300).ToString("o"), level = "AUDIT", message = $"Impacto monetario estimado: ${inc.MonetaryImpact:N2} COP | Tag sugerido: {inc.SuggestedTag}" }
        };

        return Ok(new
        {
            InconsistencyId    = inc.InconsistencyId,
            ExecutionId        = inc.ExecutionId,
            Component          = inc.Component,
            MonetaryImpact     = inc.MonetaryImpact,
            FieldAffected      = inc.FieldAffected,
            SuggestedTag       = inc.SuggestedTag,
            Description        = inc.Description,
            ExpectedValueJson  = inc.ExpectedValueJson,
            ActualValueJson    = inc.ActualValueJson,
            DetectedAt         = inc.DetectedAt,
            TenantId           = inc.TenantId,
            HasScreenshot      = inc.HasScreenshot,
            // ── Persistencia y Carga Perezosa: Solo se entrega al abrir el Drawer ──
            ScreenshotBase64   = inc.ScreenshotBase64,
            ContextualLogs     = contextualTrace
        });
    }
}
