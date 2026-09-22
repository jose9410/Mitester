using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MiTesterE2E.Persistence.Multitenancy;

namespace MiTesterE2E.Persistence.Entities;

/// <summary>
/// Entidad de persistencia para el registro de corridas y ejecuciones E2E.
/// </summary>
[Table("Executions")]
public class ExecutionEntity : ITenantEntity
{
    [Key]
    [MaxLength(100)]
    public string ExecutionId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ProcessId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "PENDING";

    [Column(TypeName = "decimal(5,2)")]
    public decimal ConsistencyPercentage { get; set; }

    public long TotalTransactions { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    [Required]
    [MaxLength(100)]
    public string TenantId { get; set; } = string.Empty;

    // Relación 1:N con Inconsistencias
    public ICollection<InconsistencyEntity> Inconsistencies { get; set; } = new List<InconsistencyEntity>();
}
