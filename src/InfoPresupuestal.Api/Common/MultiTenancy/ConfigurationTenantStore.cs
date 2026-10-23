using Microsoft.Extensions.Options;

namespace InfoPresupuestal.Api.Common.MultiTenancy;

internal sealed class ConfigurationTenantStore(IOptionsMonitor<MultiTenancyOptions> options) : ITenantStore
{
    public Task<bool> ExistsAsync(string tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(options.CurrentValue.Tenants.Any(t => string.Equals(t.Id, tenantId, StringComparison.Ordinal)));
}
