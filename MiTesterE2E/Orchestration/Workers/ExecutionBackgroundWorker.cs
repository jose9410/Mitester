using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MiTesterE2E.Orchestration.Contracts;
using MiTesterE2E.Orchestration.Services;
using MiTesterE2E.Telemetry;
using MiTesterE2E.Telemetry.Contracts;

namespace MiTesterE2E.Orchestration.Workers;

/// <summary>
/// Servicio en segundo plano que consume tareas del Channel y emite telemetría
/// en tiempo real hacia el frontend Angular a través del TelemetryHub de SignalR.
///
/// Ciclo de vida:
///   1. Se inicia automáticamente con el host (IHostedService / BackgroundService).
///   2. Permanece activo en un bucle hasta que el host solicita shutdown.
///   3. Para cada ExecutionTask dequeued:
///      a. Parsea el JSON para extraer metadatos (tenant, procesos, pasos).
///      b. Simula el procesamiento en 10 ticks de 10% cada uno con Task.Delay.
///      c. En cada tick emite "ExecutionProgressUpdated" a TODOS los clientes SignalR.
///      d. Al finalizar emite "ExecutionCompletedToast" a TODOS los clientes SignalR.
///
/// Nota: En Fase 1 no hay integración real con Oracle/SQL Server.
/// La simulación usa los metadatos del JSON (processId, steps) para construir
/// mensajes de progreso descriptivos y realistas.
/// </summary>
public sealed class ExecutionBackgroundWorker : BackgroundService
{
    private const int ProgressTicks = 10;
    private readonly IExecutionTaskQueue _queue;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ILogger<ExecutionBackgroundWorker> _logger;
    private readonly int _progressDelayMs;

    public ExecutionBackgroundWorker(
        IExecutionTaskQueue queue,
        IHubContext<TelemetryHub> hubContext,
        IConfiguration configuration,
        ILogger<ExecutionBackgroundWorker> logger)
    {
        _queue          = queue;
        _hubContext     = hubContext;
        _logger         = logger;
        _progressDelayMs = configuration.GetValue<int>("Orchestration:SimulatedProgressDelayMs", 500);
    }

    /// <summary>
    /// Bucle principal del BackgroundService.
    /// Se cancela automáticamente cuando el host envía la señal de shutdown (Ctrl+C / ACA stop).
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[ExecutionBackgroundWorker] Worker iniciado. Escuchando tareas en el Channel...");

        // El bucle se repite indefinidamente hasta que stoppingToken sea cancelado
        while (!stoppingToken.IsCancellationRequested)
        {
            ExecutionTask? task = null;

            try
            {
                // Se bloquea asincrónicamente hasta que llegue una tarea o se cancele el host
                task = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown graceful del host — salimos del bucle limpiamente
                _logger.LogInformation(
                    "[ExecutionBackgroundWorker] Shutdown solicitado. Deteniendo worker.");
                break;
            }

            // Procesar la tarea fuera del try-catch del token para capturar errores reales
            await ProcessExecutionTaskAsync(task, stoppingToken);
        }

        _logger.LogInformation("[ExecutionBackgroundWorker] Worker detenido.");
    }

    /// <summary>
    /// Procesa una única tarea de ejecución: simula el progreso y emite eventos SignalR.
    /// </summary>
    private async Task ProcessExecutionTaskAsync(ExecutionTask task, CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[ExecutionBackgroundWorker] Iniciando procesamiento. ExecutionId: {Id} | Encolada hace: {Lag}ms",
            task.ExecutionId,
            (DateTimeOffset.UtcNow - task.EnqueuedAt).TotalMilliseconds);

        // Extraer metadatos del JSON para construir mensajes descriptivos
        var suiteMetadata = ExtractSuiteMetadata(task.SuiteConfigJson);

        try
        {
            // ── BUCLE DE PROGRESO: 10 ticks de 10% ───────────────────────────────
            for (int tick = 1; tick <= ProgressTicks; tick++)
            {
                if (stoppingToken.IsCancellationRequested) break;

                // Esperar el delay simulado
                await Task.Delay(_progressDelayMs, stoppingToken);

                var percentage  = tick * (100 / ProgressTicks);
                var description = BuildProgressDescription(tick, ProgressTicks, suiteMetadata);

                _logger.LogInformation(
                    "[ExecutionBackgroundWorker] Progreso {Pct}% | ExecutionId: {Id} | Paso: {Desc}",
                    percentage, task.ExecutionId, description);

                // ── EMITIR EVENTO SignalR → "ExecutionProgressUpdated" ──────────
                await _hubContext.Clients.All.SendAsync(
                    "ExecutionProgressUpdated",
                    new ProgressUpdatedPayload
                    {
                        ExecutionId          = task.ExecutionId,
                        ProgressPercentage   = percentage,
                        ActiveStepDescription = description
                    },
                    stoppingToken);
            }

            stopwatch.Stop();

            var completionMessage = $"Ejecución '{suiteMetadata.Application}' completada. " +
                                    $"{suiteMetadata.TotalSteps:N0} pasos procesados en {suiteMetadata.ProcessCount} proceso(s). " +
                                    $"Tenant: {suiteMetadata.Tenant}";

            _logger.LogInformation(
                "[ExecutionBackgroundWorker] Ejecución completada. ExecutionId: {Id} | Duración: {Dur}s",
                task.ExecutionId,
                stopwatch.Elapsed.TotalSeconds);

            // ── EMITIR EVENTO SignalR → "ExecutionCompletedToast" ──────────────
            await _hubContext.Clients.All.SendAsync(
                "ExecutionCompletedToast",
                new ExecutionCompletedPayload
                {
                    ExecutionId     = task.ExecutionId,
                    FinalStatus     = "COMPLETED_SUCCESS",
                    Message         = completionMessage,
                    DurationSeconds = stopwatch.Elapsed.TotalSeconds
                },
                stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown durante el procesamiento — no es un error real
            _logger.LogWarning(
                "[ExecutionBackgroundWorker] Ejecución interrumpida por shutdown. ExecutionId: {Id}",
                task.ExecutionId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "[ExecutionBackgroundWorker] Error durante el procesamiento. ExecutionId: {Id}",
                task.ExecutionId);

            // Notificar al frontend del fallo para que muestre un Toast de error
            await _hubContext.Clients.All.SendAsync(
                "ExecutionCompletedToast",
                new ExecutionCompletedPayload
                {
                    ExecutionId     = task.ExecutionId,
                    FinalStatus     = "FAILED",
                    Message         = $"Error en la ejecución '{suiteMetadata.Application}': {ex.Message}",
                    DurationSeconds = stopwatch.Elapsed.TotalSeconds
                },
                CancellationToken.None); // No usar stoppingToken aquí para asegurar la notificación de error
        }
    }

    /// <summary>
    /// Construye el texto descriptivo del paso activo para el evento de progreso.
    /// Usa los metadatos del JSON para hacerlo lo más realista posible.
    /// </summary>
    private static string BuildProgressDescription(int tick, int totalTicks, SuiteMetadata meta)
    {
        // Distribuye los ticks entre los procesos de la suite de forma proporcional
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

    /// <summary>
    /// Extrae metadatos básicos del JSON de la suite para enriquecer los mensajes de progreso.
    /// Si el JSON es inválido o está vacío, retorna valores por defecto para no interrumpir el flujo.
    /// </summary>
    private SuiteMetadata ExtractSuiteMetadata(string suiteConfigJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(suiteConfigJson);
            var root = doc.RootElement;

            var tenant      = root.TryGetProperty("tenant",      out var t) ? t.GetString() ?? "N/A" : "N/A";
            var environment = root.TryGetProperty("environment", out var e) ? e.GetString() ?? "N/A" : "N/A";
            var application = root.TryGetProperty("application", out var a) ? a.GetString() ?? "N/A" : "N/A";

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
                "[ExecutionBackgroundWorker] No se pudo parsear el JSON de la suite para metadatos. " +
                "Error: {Msg}. Se usarán valores por defecto.",
                ex.Message);

            return new SuiteMetadata("N/A", "N/A", "Suite E2E", [], 1, 1);
        }
    }

    /// <summary>
    /// Metadatos extraídos del JSON de la suite.
    /// Record para inmutabilidad y sintaxis compacta.
    /// </summary>
    private sealed record SuiteMetadata(
        string Tenant,
        string Environment,
        string Application,
        List<string> ProcessIds,
        int ProcessCount,
        int TotalSteps);
}
