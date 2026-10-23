namespace InfoPresupuestal.Api.Common.MultiTenancy;

/// <summary>Catálogo de tenants válidos. Hoy viene de configuración; puede pasar a una tabla sin tocar a sus consumidores.</summary>
public interface ITenantStore
{
    Task<bool> ExistsAsync(string tenantId, CancellationToken cancellationToken);
}
