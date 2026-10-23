using InfoPresupuestal.Api.Common.Caching;

namespace InfoPresupuestal.Api.Common.MultiTenancy;

/// <summary>
/// Decorador de <see cref="ICacheService"/> que antepone el tenant a cada clave, así dos tenants
/// nunca comparten una entrada aunque el slice use la misma clave.
/// </summary>
internal sealed class TenantCacheService(ICacheService inner, ITenantContext tenantContext) : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        inner.GetAsync<T>(Scoped(key), cancellationToken);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) =>
        inner.SetAsync(Scoped(key), value, expiration, cancellationToken);

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        inner.RemoveAsync(Scoped(key), cancellationToken);

    private string Scoped(string key) => $"tenant:{tenantContext.TenantId}:{key}";
}
