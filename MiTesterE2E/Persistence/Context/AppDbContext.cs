using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MiTesterE2E.Persistence.Entities;
using MiTesterE2E.Persistence.Multitenancy;

namespace MiTesterE2E.Persistence.Context;

/// <summary>
/// DbContext personalizado con soporte para aislamiento Multitenant Nivel 1 (Columna Discriminadora + Global Query Filters).
/// </summary>
public class AppDbContext : DbContext
{
    private readonly ITenantService _tenantService;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantService tenantService)
        : base(options)
    {
        _tenantService = tenantService;
    }

    /// <summary>
    /// Propiedad pública que expone el Tenant actual para que EF Core pueda parametrizar los Global Query Filters.
    /// </summary>
    public string CurrentTenantId => _tenantService.CurrentTenantId;

    public DbSet<ExecutionEntity> Executions => Set<ExecutionEntity>();
    public DbSet<InconsistencyEntity> Inconsistencies => Set<InconsistencyEntity>();
    public DbSet<ScorecardMetricsEntity> ScorecardMetrics => Set<ScorecardMetricsEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Configuración de Relaciones e Índices
        modelBuilder.Entity<ExecutionEntity>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ProcessId });
            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.CreatedAt });

            entity.HasMany(e => e.Inconsistencies)
                  .WithOne(i => i.Execution)
                  .HasForeignKey(i => i.ExecutionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InconsistencyEntity>(entity =>
        {
            entity.HasIndex(i => new { i.TenantId, i.ExecutionId });
            entity.HasIndex(i => new { i.TenantId, i.Component });
        });

        modelBuilder.Entity<ScorecardMetricsEntity>(entity =>
        {
            entity.HasIndex(s => new { s.TenantId, s.ProcessId });
        });

        // 2. Registro Automático de Global Query Filters para todas las entidades ITenantEntity
        var configureTenantFilterMethod = typeof(AppDbContext)
            .GetMethod(nameof(ConfigureTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                configureTenantFilterMethod?
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    /// <summary>
    /// Aplica el filtro global LINQ (WHERE TenantId == CurrentTenantId) de forma fuertemente tipada.
    /// </summary>
    private void ConfigureTenantFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    /// <summary>
    /// Intercepta las operaciones de guardado para inyectar automáticamente el TenantId activo.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenantIdToAddedEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyTenantIdToAddedEntities();
        return base.SaveChanges();
    }

    private void ApplyTenantIdToAddedEntities()
    {
        var tenantId = _tenantService.CurrentTenantId;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                // Solo asigna si no fue explicitado previamente
                if (string.IsNullOrWhiteSpace(entry.Entity.TenantId))
                {
                    entry.Entity.TenantId = tenantId;
                }
            }
        }
    }
}
