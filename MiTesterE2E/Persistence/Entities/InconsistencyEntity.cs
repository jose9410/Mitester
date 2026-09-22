using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MiTesterE2E.Persistence.Multitenancy;

namespace MiTesterE2E.Persistence.Entities;

/// <summary>
/// Entidad de persistencia para el registro y triaje de inconsistencias transaccionales.
/// </summary>
[Table("Inconsistencies")]
public class InconsistencyEntity : ITenantEntity
{
    [Key]
    [MaxLength(100)]
    public string InconsistencyId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ExecutionId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Component { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MonetaryImpact { get; set; }

    [Required]
    [MaxLength(150)]
    public string FieldAffected { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string SuggestedTag { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(4000)]
    public string? ExpectedValueJson { get; set; }

    [MaxLength(4000)]
    public string? ActualValueJson { get; set; }

    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Indica si existe evidencia visual en captura de pantalla asociada a esta inconsistencia.
    /// </summary>
    public bool HasScreenshot { get; set; }

    /// <summary>
    /// Evidencia fotográfica del fallo comprimida en JPEG (75%) codificada en Base64 (máx 250KB).
    /// Carga perezosa (Lazy Loading) únicamente bajo demanda en el Triage Drawer.
    /// </summary>
    public string? ScreenshotBase64 { get; set; }

    [Required]
    [MaxLength(100)]
    public string TenantId { get; set; } = string.Empty;

    [ForeignKey(nameof(ExecutionId))]
    public ExecutionEntity? Execution { get; set; }
}
