namespace InfoPresupuestal.Api.Common.MultiTenancy;

/// <summary>Tenant de la solicitud actual. Lo leen la persistencia y la caché; los handlers no lo necesitan.</summary>
public interface ITenantContext
{
    /// <summary>Id del tenant. Lanza si todavía no se resolvió, para que nada se ejecute sin tenant.</summary>
    string TenantId { get; }

    bool HasTenant { get; }
}
