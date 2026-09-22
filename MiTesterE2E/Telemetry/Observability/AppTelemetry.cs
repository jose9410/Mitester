using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MiTesterE2E.Telemetry.Observability;

/// <summary>
/// Provee los puntos de instrumentación centrales para OpenTelemetry:
/// ActivitySource (Trazado distribuido) y Meter (Métricas personalizadas).
/// </summary>
public static class AppTelemetry
{
    public const string ServiceName = "MiTesterE2E";
    public const string ServiceVersion = "1.1.0";

    // ─────────────────────────────────────────────────────────────────────────────
    // 1. TRACING (ActivitySource)
    // ─────────────────────────────────────────────────────────────────────────────
    public const string ActivitySourceName = "MiTesterE2E.Orchestration";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, ServiceVersion);

    // ─────────────────────────────────────────────────────────────────────────────
    // 2. METRICS (Meter)
    // ─────────────────────────────────────────────────────────────────────────────
    public const string MeterName = "MiTesterE2E.Metrics";
    public static readonly Meter Meter = new(MeterName, ServiceVersion);

    /// <summary>
    /// Contador acumulativo de transacciones bancarias procesadas y conciliadas.
    /// </summary>
    public static readonly Counter<long> TransactionsProcessedCounter = Meter.CreateCounter<long>(
        name: "mitester.transactions.processed_total",
        unit: "{transactions}",
        description: "Total acumulado de transacciones bancarias procesadas y conciliadas.");

    /// <summary>
    /// Histograma de latencia y tiempo de ejecución de scripts SQL y comandos UI Playwright (en milisegundos).
    /// </summary>
    public static readonly Histogram<double> ExecutionDurationHistogram = Meter.CreateHistogram<double>(
        name: "mitester.execution.duration_ms",
        unit: "ms",
        description: "Duración de ejecución de scripts SQL, comandos Playwright y suites de prueba.");

    /// <summary>
    /// Contador acumulativo de inconsistencias detectadas categorizadas por tipo (Monetaria, Estructural, UI).
    /// </summary>
    public static readonly Counter<long> InconsistenciesCounter = Meter.CreateCounter<long>(
        name: "mitester.inconsistencies.detected_total",
        unit: "{inconsistencies}",
        description: "Total de inconsistencias y discrepancias detectadas por tipo.");

    /// <summary>
    /// Histograma del porcentaje de consistencia alcanzado en conciliación masiva.
    /// </summary>
    public static readonly Histogram<double> ConsistencyPctHistogram = Meter.CreateHistogram<double>(
        name: "mitester.reconciliation.consistency_pct",
        unit: "%",
        description: "Porcentaje de consistencia alcanzado en conciliaciones de datos masivos.");
}
