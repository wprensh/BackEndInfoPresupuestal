namespace InfoPresupuestal.Api.Common.Caching;

/// <summary>Abstracción de caché. Los slices dependen de esta interfaz, no de Redis.</summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
