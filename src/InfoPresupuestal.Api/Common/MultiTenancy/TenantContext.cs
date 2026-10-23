namespace InfoPresupuestal.Api.Common.MultiTenancy;

/// <summary>Implementación scoped: una instancia por solicitud, la llena <see cref="TenantResolutionFilter"/>.</summary>
public sealed class TenantContext : ITenantContext
{
    private string? _tenantId;

    public string TenantId => _tenantId
        ?? throw new InvalidOperationException("No hay un tenant resuelto para la solicitud actual.");

    public bool HasTenant => _tenantId is not null;

    public void SetTenant(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (_tenantId is not null && _tenantId != tenantId)
        {
            throw new InvalidOperationException("El tenant de la solicitud ya fue asignado.");
        }

        _tenantId = tenantId;
    }
}
