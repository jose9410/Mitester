using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MiTesterE2E.Persistence.Multitenancy;

namespace MiTesterE2E.Persistence.Entities;

/// <summary>
/// Entidad de persistencia para el consolidado del Dashboard Scorecard bancario.
/// </summary>
[Table("ScorecardMetrics")]
public class ScorecardMetricsEntity : ITenantEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string ProcessId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string CurrentVersion { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? PreviousVersion { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal ConsistencyPercentage { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal AcceptanceThreshold { get; set; } = 99.50m;

    [Column(TypeName = "decimal(5,2)")]
    public decimal Delta { get; set; }

    [Required]
    [MaxLength(50)]
    public string StatusBadge { get; set; } = "PASS";

    public long TotalTransactionsProcessed { get; set; }

    public int DiscrepanciesCount { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    [MaxLength(100)]
    public string TenantId { get; set; } = string.Empty;
}
