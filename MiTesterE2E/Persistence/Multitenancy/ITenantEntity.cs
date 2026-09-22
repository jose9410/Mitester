namespace MiTesterE2E.Persistence.Multitenancy;

/// <summary>
/// Contrato obligatorio para todas las entidades que requieren aislamiento Multitenant (Nivel 1: Columna Discriminadora).
/// </summary>
public interface ITenantEntity
{
    /// <summary>
    /// Identificador del Tenant propietario del registro.
    /// </summary>
    string TenantId { get; set; }
}
