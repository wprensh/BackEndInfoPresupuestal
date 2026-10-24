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

    /// <summary>
    /// Cambia de tenant temporalmente y lo restaura al liberar el resultado. Solo para procesos de
    /// plataforma explícitos (crear una empresa, sembrar datos), nunca para atender una solicitud normal.
    /// </summary>
    public IDisposable Use(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var previous = _tenantId;
        _tenantId = tenantId;
        return new Restore(this, previous);
    }

    private sealed class Restore(TenantContext context, string? previous) : IDisposable
    {
        public void Dispose() => context._tenantId = previous;
    }
}
