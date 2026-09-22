namespace MiTesterE2E.Persistence.Multitenancy;

/// <summary>
/// Contrato para resolver e identificar el Tenant (entidad bancaria) activo en el contexto de ejecución.
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Identificador del Tenant actual (ej. "BANCO_NACIONAL", "BANCO_REGIONAL", "DEFAULT_TENANT").
    /// </summary>
    string CurrentTenantId { get; }

    /// <summary>
    /// Permite sobreescribir o fijar explícitamente el Tenant en contextos en segundo plano (Background Workers).
    /// </summary>
    /// <param name="tenantId">Identificador del tenant.</param>
    void SetTenant(string tenantId);
}
