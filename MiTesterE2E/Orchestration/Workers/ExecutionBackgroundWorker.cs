using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiTesterE2E.Automation.Contracts;
using MiTesterE2E.Automation.Services;
using MiTesterE2E.Orchestration.Contracts;
using MiTesterE2E.Orchestration.Services;
using MiTesterE2E.Persistence.Context;
using MiTesterE2E.Persistence.Entities;
using MiTesterE2E.Persistence.Multitenancy;
using MiTesterE2E.Telemetry;
using MiTesterE2E.Telemetry.Contracts;

namespace MiTesterE2E.Orchestration.Workers;

/// <summary>
/// Servicio en segundo plano que orquesta la ejecución física de pruebas UI con Playwright
/// o la simulación de procesos masivos, emitiendo telemetría en tiempo real hacia SignalR.
/// </summary>
public sealed class ExecutionBackgroundWorker : BackgroundService
{
    private const int DefaultSimulationTicks = 10;
    private readonly IExecutionTaskQueue _queue;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly IPlaywrightCommandExecutor _playwrightExecutor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExecutionBackgroundWorker> _logger;
    private readonly int _progressDelayMs;

    public ExecutionBackgroundWorker(
        IExecutionTaskQueue queue,
        IHubContext<TelemetryHub> hubContext,
        IPlaywrightCommandExecutor playwrightExecutor,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ExecutionBackgroundWorker> logger)
    {
        _queue             = queue;
        _hubContext        = hubContext;
        _playwrightExecutor = playwrightExecutor;
        _scopeFactory      = scopeFactory;
        _logger            = logger;
        _progressDelayMs   = configuration.GetValue<int>("Orchestration:SimulatedProgressDelayMs", 500);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[ExecutionBackgroundWorker] Worker iniciado. Escuchando tareas en el Channel...");

        while (!stoppingToken.IsCancellationRequested)
        {
            ExecutionTask? task = null;

            try
            {
                task = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[ExecutionBackgroundWorker] Shutdown solicitado. Deteniendo worker.");
                break;
            }

            if (task != null)
            {
                await ProcessExecutionTaskAsync(task, stoppingToken);
            }
        }

        _logger.LogInformation("[ExecutionBackgroundWorker] Worker detenido.");
    }

    private async Task ProcessExecutionTaskAsync(ExecutionTask task, CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[ExecutionBackgroundWorker] Iniciando procesamiento. ExecutionId: {Id} | Encolada hace: {Lag}ms",
            task.ExecutionId,
            (DateTimeOffset.UtcNow - task.EnqueuedAt).TotalMilliseconds);

        var suiteMetadata = ExtractSuiteMetadata(task.SuiteConfigJson);
        var uiCommands = ExtractUiCommands(task.SuiteConfigJson);

        try
        {
            if (uiCommands.Count > 0)
            {
                // ── EJECUCIÓN FÍSICA CON PLAYWRIGHT (UI_SEQUENCE) ────────────────────
                _logger.LogInformation(
                    "[ExecutionBackgroundWorker] Detectada acción UI_SEQUENCE con {Count} comandos Playwright. Iniciando motor de automatización...",
                    uiCommands.Count);

                var uiResult = await _playwrightExecutor.ExecuteSequenceAsync(
                    task.ExecutionId.ToString(),
                    uiCommands,
                    async (stepIndex, total, description) =>
                    {
                        var pct = (int)Math.Round((double)stepIndex / total * 100);
                        await _hubContext.Clients.All.SendAsync(
                            "ExecutionProgressUpdated",
                            new ProgressUpdatedPayload
                            {
                                ExecutionId           = task.ExecutionId,
                                ProgressPercentage    = pct,
                                ActiveStepDescription = description,
                                HasScreenshot         = false
                            },
                            stoppingToken);
                    },
                    stoppingToken);

                stopwatch.Stop();

                if (uiResult.Success)
                {
                    await PersistExecutionSuccessAsync(task, suiteMetadata, stopwatch.Elapsed.TotalSeconds);

                    await _hubContext.Clients.All.SendAsync(
                        "ExecutionCompletedToast",
                        new ExecutionCompletedPayload
                        {
                            ExecutionId     = task.ExecutionId,
                            FinalStatus     = "COMPLETED_SUCCESS",
                            Message         = $"Secuencia UI Playwright '{suiteMetadata.Application}' completada exitosamente ({uiResult.TotalCommands} comandos ejecutados).",
                            DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                            HasScreenshot   = false
                        },
                        stoppingToken);
                }
                else
                {
                    // Fallo en UI: Guardar inconsistencia con evidencia Base64 y emitir Toast
                    var inconsistencyId = $"INC-UI-{task.ExecutionId.ToString()[..8].ToUpperInvariant()}";

                    await PersistUiFailureAsync(task, suiteMetadata, uiResult, inconsistencyId);

                    await _hubContext.Clients.All.SendAsync(
                        "ExecutionCompletedToast",
                        new ExecutionCompletedPayload
                        {
                            ExecutionId     = task.ExecutionId,
                            FinalStatus     = "FAILED",
                            Message         = $"Fallo en automatización UI '{suiteMetadata.Application}': {uiResult.ErrorMessage}",
                            DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                            HasScreenshot   = uiResult.HasScreenshot,
                            InconsistencyId = inconsistencyId
                        },
                        stoppingToken);
                }
            }
            else
            {
                // ── MODO SIMULACIÓN PROGRESIVA (Procesos masivos / Conciliaciones) ──
                for (int tick = 1; tick <= DefaultSimulationTicks; tick++)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    await Task.Delay(_progressDelayMs, stoppingToken);

                    var percentage  = tick * (100 / DefaultSimulationTicks);
                    var description = BuildProgressDescription(tick, DefaultSimulationTicks, suiteMetadata);

                    await _hubContext.Clients.All.SendAsync(
                        "ExecutionProgressUpdated",
                        new ProgressUpdatedPayload
                        {
                            ExecutionId           = task.ExecutionId,
                            ProgressPercentage    = percentage,
                            ActiveStepDescription = description,
                            HasScreenshot         = false
                        },
                        stoppingToken);
                }

                stopwatch.Stop();
                await PersistExecutionSuccessAsync(task, suiteMetadata, stopwatch.Elapsed.TotalSeconds);

                var completionMessage = $"Ejecución '{suiteMetadata.Application}' completada. " +
                                        $"{suiteMetadata.TotalSteps:N0} pasos procesados en {suiteMetadata.ProcessCount} proceso(s). " +
                                        $"Tenant: {suiteMetadata.Tenant}";

                await _hubContext.Clients.All.SendAsync(
                    "ExecutionCompletedToast",
                    new ExecutionCompletedPayload
                    {
                        ExecutionId     = task.ExecutionId,
                        FinalStatus     = "COMPLETED_SUCCESS",
                        Message         = completionMessage,
                        DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                        HasScreenshot   = false
                    },
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "[ExecutionBackgroundWorker] Ejecución interrumpida por shutdown. ExecutionId: {Id}",
                task.ExecutionId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "[ExecutionBackgroundWorker] Error general durante el procesamiento. ExecutionId: {Id}",
                task.ExecutionId);

            await _hubContext.Clients.All.SendAsync(
                "ExecutionCompletedToast",
                new ExecutionCompletedPayload
                {
                    ExecutionId     = task.ExecutionId,
                    FinalStatus     = "FAILED",
                    Message         = $"Error crítico en ejecución '{suiteMetadata.Application}': {ex.Message}",
                    DurationSeconds = stopwatch.Elapsed.TotalSeconds,
                    HasScreenshot   = false
                },
                CancellationToken.None);
        }
    }

    private async Task PersistExecutionSuccessAsync(ExecutionTask task, SuiteMetadata meta, double durationSec)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();

        tenantService.SetTenant(meta.Tenant);

        var execution = new ExecutionEntity
        {
            ExecutionId = task.ExecutionId.ToString(),
            ProcessId = meta.ProcessIds.FirstOrDefault() ?? "PRC-DEFAULT",
            Status = "COMPLETED_SUCCESS",
            ConsistencyPercentage = 100.00m,
            TotalTransactions = meta.TotalSteps * 1000,
            CreatedAt = task.EnqueuedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            TenantId = meta.Tenant
        };

        context.Executions.Add(execution);
        await context.SaveChangesAsync();
    }

    private async Task PersistUiFailureAsync(
        ExecutionTask task,
        SuiteMetadata meta,
        UiExecutionResult uiResult,
        string inconsistencyId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();

        tenantService.SetTenant(meta.Tenant);

        var execution = new ExecutionEntity
        {
            ExecutionId = task.ExecutionId.ToString(),
            ProcessId = meta.ProcessIds.FirstOrDefault() ?? "PRC-UI-AUTOMATION",
            Status = "FAILED",
            ConsistencyPercentage = 0.00m,
            TotalTransactions = uiResult.TotalCommands,
            CreatedAt = task.EnqueuedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            TenantId = meta.Tenant
        };

        var inconsistency = new InconsistencyEntity
        {
            InconsistencyId = inconsistencyId,
            ExecutionId = execution.ExecutionId,
            Component = "PLAYWRIGHT_CHROMIUM_ENGINE",
            MonetaryImpact = 0.00m,
            FieldAffected = uiResult.FailedCommand?.Selector ?? uiResult.FailedCommand?.Url ?? "DOM_ELEMENT",
            SuggestedTag = "ERROR_UI_PLAYWRIGHT",
            Description = $"Fallo en comando '{uiResult.FailedCommand?.CommandType}': {uiResult.ErrorMessage}",
            ExpectedValueJson = JsonSerializer.Serialize(uiResult.FailedCommand),
            ActualValueJson = JsonSerializer.Serialize(new { error = uiResult.ErrorMessage, step = uiResult.ExecutedCommandsCount }),
            DetectedAt = DateTimeOffset.UtcNow,
            TenantId = meta.Tenant,
            HasScreenshot = uiResult.HasScreenshot,
            // ── Persistencia y Carga Perezosa: Solo se almacena en BD, no en WebSocket ──
            ScreenshotBase64 = uiResult.ScreenshotBase64
        };

        context.Executions.Add(execution);
        context.Inconsistencies.Add(inconsistency);
        await context.SaveChangesAsync();
    }

    private static List<UiCommandDto> ExtractUiCommands(string suiteConfigJson)
    {
        var list = new List<UiCommandDto>();
        try
        {
            using var doc = JsonDocument.Parse(suiteConfigJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("processes", out var processes) && processes.ValueKind == JsonValueKind.Array)
            {
                foreach (var process in processes.EnumerateArray())
                {
                    if (process.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var step in steps.EnumerateArray())
                        {
                            var actionType = step.TryGetProperty("actionType", out var at) ? at.GetString() : null;
                            if (string.Equals(actionType, "UI_SEQUENCE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (step.TryGetProperty("payload", out var payload) &&
                                    payload.TryGetProperty("uiCommands", out var commands) &&
                                    commands.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var cmd in commands.EnumerateArray())
                                    {
                                        var dto = JsonSerializer.Deserialize<UiCommandDto>(cmd.GetRawText());
                                        if (dto != null) list.Add(dto);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignorar errores de parsing si no contiene comandos válidos
        }

        return list;
    }

    private static string BuildProgressDescription(int tick, int totalTicks, SuiteMetadata meta)
    {
        var processIndex = Math.Min(
            (int)Math.Floor((tick - 1.0) / totalTicks * meta.ProcessCount),
            meta.ProcessCount - 1);

        var processLabel = meta.ProcessIds.Count > processIndex
            ? meta.ProcessIds[processIndex]
            : $"Proceso {processIndex + 1}";

        return tick switch
        {
            1  => $"Inicializando motor E2E — Tenant: {meta.Tenant}",
            2  => $"Validando configuración de entorno: {meta.Environment}",
            >= 3 and <= 8 => $"Procesando lote {tick - 2}/{totalTicks - 4} — {processLabel} ({meta.Application})",
            9  => "Ejecutando reglas de negocio y aserciones finales",
            10 => "Consolidando resultados y generando métricas de conciliación",
            _  => $"Procesando tick {tick}/{totalTicks}"
        };
    }

    private SuiteMetadata ExtractSuiteMetadata(string suiteConfigJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(suiteConfigJson);
            var root = doc.RootElement;

            var tenant      = root.TryGetProperty("tenant",      out var t) ? t.GetString() ?? "DEFAULT_TENANT" : "DEFAULT_TENANT";
            var environment = root.TryGetProperty("environment", out var e) ? e.GetString() ?? "DEV" : "DEV";
            var application = root.TryGetProperty("application", out var a) ? a.GetString() ?? "Suite E2E" : "Suite E2E";

            var processIds = new List<string>();
            int totalSteps = 0;

            if (root.TryGetProperty("processes", out var processes) && processes.ValueKind == JsonValueKind.Array)
            {
                foreach (var process in processes.EnumerateArray())
                {
                    if (process.TryGetProperty("processId", out var pid))
                        processIds.Add(pid.GetString() ?? "?");

                    if (process.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
                        totalSteps += steps.GetArrayLength();
                }
            }

            return new SuiteMetadata(
                Tenant:       tenant,
                Environment:  environment,
                Application:  application,
                ProcessIds:   processIds,
                ProcessCount: Math.Max(processIds.Count, 1),
                TotalSteps:   Math.Max(totalSteps, 1));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                "[ExecutionBackgroundWorker] No se pudo parsear el JSON de la suite para metadatos: {Msg}",
                ex.Message);

            return new SuiteMetadata("DEFAULT_TENANT", "DEV", "Suite E2E", [], 1, 1);
        }
    }

    private sealed record SuiteMetadata(
        string Tenant,
        string Environment,
        string Application,
        List<string> ProcessIds,
        int ProcessCount,
        int TotalSteps);
}
