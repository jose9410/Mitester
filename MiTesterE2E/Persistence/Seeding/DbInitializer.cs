using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiTesterE2E.Persistence.Context;
using MiTesterE2E.Persistence.Entities;
using MiTesterE2E.Persistence.Multitenancy;

namespace MiTesterE2E.Persistence.Seeding;

/// <summary>
/// Inicializador y semillero de datos bancarios para pruebas de aislamiento Multitenant.
/// (Se removerá en fases posteriores de producción).
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            // Garantizar que la base de datos esté creada
            await context.Database.EnsureCreatedAsync();

            // Usamos IgnoreQueryFilters() para verificar existencia global de datos
            if (await context.Executions.IgnoreQueryFilters().AnyAsync())
            {
                logger.LogInformation("[DbInitializer] La base de datos ya contiene registros. Omitiendo seed.");
                return;
            }

            logger.LogInformation("[DbInitializer] Sembrando datos iniciales multitenant para pruebas bancarias...");

            // ─────────────────────────────────────────────────────────────────────────
            // 1. TENANT A: BANCO_NACIONAL (Caso con discrepancias < 99.5%)
            // ─────────────────────────────────────────────────────────────────────────
            tenantService.SetTenant("BANCO_NACIONAL");

            var execBancoNacional = new ExecutionEntity
            {
                ExecutionId = "EXEC-2026-BN-001",
                ProcessId = "PRC-CONCIL-MASSIVE",
                Status = "COMPLETED_WARNING",
                ConsistencyPercentage = 99.12m,
                TotalTransactions = 1500000,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-45),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-40),
                TenantId = "BANCO_NACIONAL"
            };

            var inconstBN1 = new InconsistencyEntity
            {
                InconsistencyId = "INC-BN-001",
                ExecutionId = execBancoNacional.ExecutionId,
                Component = "MOTOR_CONCILIACION_CORE",
                MonetaryImpact = 45230.50m,
                FieldAffected = "TOTAL_DEPOSIT_AMOUNT",
                SuggestedTag = "DISCREPANCIA_REDONDEO",
                Description = "Diferencia de centavos acumulada en lote de depósitos masivos ACH.",
                ExpectedValueJson = "{\"currency\": \"COP\", \"totalExpected\": 152000000.00, \"tolerance\": 0.00}",
                ActualValueJson = "{\"currency\": \"COP\", \"totalObtained\": 151954769.50, \"diff\": -45230.50}",
                TenantId = "BANCO_NACIONAL"
            };

            var inconstBN2 = new InconsistencyEntity
            {
                InconsistencyId = "INC-BN-002",
                ExecutionId = execBancoNacional.ExecutionId,
                Component = "SWIFT_GATEWAY_OUTBOUND",
                MonetaryImpact = 120500.00m,
                FieldAffected = "COMMISSION_TAX_RETENTION",
                SuggestedTag = "ERROR_CALCULO_IVA",
                Description = "Retención de impuestos no aplicada en transferencias internacionales.",
                ExpectedValueJson = "{\"taxRate\": 0.19, \"withheld\": 120500.00}",
                ActualValueJson = "{\"taxRate\": 0.00, \"withheld\": 0.00}",
                TenantId = "BANCO_NACIONAL"
            };

            var scorecardBN = new ScorecardMetricsEntity
            {
                ProcessId = "PRC-CONCIL-MASSIVE",
                CurrentVersion = "v2.4.1",
                PreviousVersion = "v2.3.9",
                ConsistencyPercentage = 99.12m,
                AcceptanceThreshold = 99.50m,
                Delta = -0.38m,
                StatusBadge = "FAIL",
                TotalTransactionsProcessed = 1500000,
                DiscrepanciesCount = 2,
                TenantId = "BANCO_NACIONAL"
            };

            context.Executions.Add(execBancoNacional);
            context.Inconsistencies.AddRange(inconstBN1, inconstBN2);
            context.ScorecardMetrics.Add(scorecardBN);

            // ─────────────────────────────────────────────────────────────────────────
            // 2. TENANT B: BANCO_REGIONAL (Caso exitoso >= 99.5%)
            // ─────────────────────────────────────────────────────────────────────────
            tenantService.SetTenant("BANCO_REGIONAL");

            var execBancoRegional = new ExecutionEntity
            {
                ExecutionId = "EXEC-2026-BR-001",
                ProcessId = "PRC-CLEARING-DAILY",
                Status = "COMPLETED_SUCCESS",
                ConsistencyPercentage = 99.88m,
                TotalTransactions = 850000,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
                CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-16),
                TenantId = "BANCO_REGIONAL"
            };

            var scorecardBR = new ScorecardMetricsEntity
            {
                ProcessId = "PRC-CLEARING-DAILY",
                CurrentVersion = "v1.8.0",
                PreviousVersion = "v1.7.5",
                ConsistencyPercentage = 99.88m,
                AcceptanceThreshold = 99.50m,
                Delta = 0.15m,
                StatusBadge = "PASS",
                TotalTransactionsProcessed = 850000,
                DiscrepanciesCount = 0,
                TenantId = "BANCO_REGIONAL"
            };

            context.Executions.Add(execBancoRegional);
            context.ScorecardMetrics.Add(scorecardBR);

            await context.SaveChangesAsync();
            logger.LogInformation("[DbInitializer] Semillado completado con éxito.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[DbInitializer] Nota: No se pudo conectar a Azure SQL o sembrar datos (puede operar con InMemory o cadena configurada).");
        }
    }
}
