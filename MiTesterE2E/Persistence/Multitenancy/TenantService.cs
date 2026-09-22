using Microsoft.AspNetCore.Http;

namespace MiTesterE2E.Persistence.Multitenancy;

/// <summary>
/// Implementación de resolución de Tenant basada en HttpContext (Headers, QueryParams o Fallback).
/// Registrado con ciclo de vida Scoped para aislar cada petición HTTP.
/// </summary>
public class TenantService : ITenantService
{
    private const string DefaultTenant = "DEFAULT_TENANT";
    private const string TenantHeaderKey = "X-Tenant-ID";
    private const string TenantQueryKey = "tenant";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? _explicitTenantId;

    public TenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string CurrentTenantId => ResolveTenantId();

    public void SetTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("El TenantId no puede ser nulo o vacío.", nameof(tenantId));

        _explicitTenantId = tenantId;
    }

    private string ResolveTenantId()
    {
        // 1. Si fue fijado explícitamente en el scope (ej. por BackgroundWorker)
        if (!string.IsNullOrWhiteSpace(_explicitTenantId))
            return _explicitTenantId;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return DefaultTenant;

        // 2. Revisar Header HTTP: X-Tenant-ID
        if (httpContext.Request.Headers.TryGetValue(TenantHeaderKey, out var headerVal) &&
            !string.IsNullOrWhiteSpace(headerVal))
        {
            return headerVal.ToString().Trim();
        }

        // 3. Revisar Query String: ?tenant=...
        if (httpContext.Request.Query.TryGetValue(TenantQueryKey, out var queryVal) &&
            !string.IsNullOrWhiteSpace(queryVal))
        {
            return queryVal.ToString().Trim();
        }

        // 4. Fallback seguro por defecto
        return DefaultTenant;
    }
}
