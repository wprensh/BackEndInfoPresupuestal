namespace InfoPresupuestal.Api.Common.MultiTenancy;

/// <summary>Catálogo de tenants activos. Lo implementa la feature Empresas (tabla seguridad.empresa).</summary>
public interface ITenantStore
{
    Task<bool> ExistsAsync(string tenantId, CancellationToken cancellationToken);
}
