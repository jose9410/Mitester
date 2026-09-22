using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiTesterE2E.Persistence.Context;
using MiTesterE2E.Persistence.Entities;
using MiTesterE2E.Persistence.Multitenancy;

namespace MiTesterE2E.Persistence.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ScorecardDataController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly ILogger<ScorecardDataController> _logger;

    public ScorecardDataController(
        AppDbContext context,
        ITenantService tenantService,
        ILogger<ScorecardDataController> logger)
    {
        _context = context;
        _tenantService = tenantService;
        _logger = logger;
    }

    /// <summary>
    /// Retorna el Tenant actual resuelto por el contexto de la petición.
    /// </summary>
    [HttpGet("tenant-info")]
    public IActionResult GetTenantInfo()
    {
        return Ok(new
        {
            CurrentTenantId = _tenantService.CurrentTenantId,
            Source = Request.Headers.ContainsKey("X-Tenant-ID") ? "Header (X-Tenant-ID)" :
                     Request.Query.ContainsKey("tenant") ? "QueryParam (?tenant=)" : "Default Fallback"
        });
    }

    /// <summary>
    /// Obtiene las métricas del Scorecard filtradas automáticamente por el Tenant actual (Global Query Filter).
    /// </summary>
    [HttpGet("metrics")]
    public async Task<ActionResult<ScorecardMetricsEntity>> GetCurrentScorecard()
    {
        _logger.LogInformation("[ScorecardData] Consultando métricas para Tenant: {TenantId}", _tenantService.CurrentTenantId);

        var metrics = await _context.ScorecardMetrics
                                    .OrderByDescending(s => s.UpdatedAt)
                                    .FirstOrDefaultAsync();

        if (metrics == null)
        {
            return NotFound(new { message = $"No se encontraron métricas para el tenant '{_tenantService.CurrentTenantId}'." });
        }

        return Ok(metrics);
    }

    /// <summary>
    /// Obtiene la lista de inconsistencias detectadas para el Tenant actual.
    /// </summary>
    [HttpGet("inconsistencies")]
    public async Task<ActionResult<IEnumerable<InconsistencyEntity>>> GetInconsistencies()
    {
        var items = await _context.Inconsistencies
                                  .OrderByDescending(i => i.DetectedAt)
                                  .ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// Obtiene el historial de ejecuciones E2E del Tenant actual.
    /// </summary>
    [HttpGet("executions")]
    public async Task<ActionResult<IEnumerable<ExecutionEntity>>> GetExecutions()
    {
        var executions = await _context.Executions
                                       .Include(e => e.Inconsistencies)
                                       .OrderByDescending(e => e.CreatedAt)
                                       .ToListAsync();

        return Ok(executions);
    }
}
