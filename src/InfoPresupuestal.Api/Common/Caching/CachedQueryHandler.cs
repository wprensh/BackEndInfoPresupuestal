using InfoPresupuestal.Api.Common.Abstractions;

namespace InfoPresupuestal.Api.Common.Caching;

/// <summary>
/// Decorador cache-aside: busca en caché, si no está ejecuta el handler real y guarda solo
/// los resultados exitosos. Si la caché falla, la consulta sigue funcionando contra la base de datos.
/// </summary>
internal sealed partial class CachedQueryHandler<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    ICacheService cache,
    ILogger<CachedQueryHandler<TQuery, TResponse>> logger) : IQueryHandler<TQuery, TResponse>
    where TQuery : ICachedQuery<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var cached = await cache.GetAsync<TResponse>(query.CacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result<TResponse>.Success(cached);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCacheReadFailed(logger, ex, query.CacheKey);
        }

        var result = await inner.HandleAsync(query, cancellationToken);

        if (result.IsSuccess)
        {
            try
            {
                await cache.SetAsync(query.CacheKey, result.Value, query.Expiration, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCacheWriteFailed(logger, ex, query.CacheKey);
            }
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo leer la clave {CacheKey} de la caché")]
    private static partial void LogCacheReadFailed(ILogger logger, Exception exception, string cacheKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo escribir la clave {CacheKey} en la caché")]
    private static partial void LogCacheWriteFailed(ILogger logger, Exception exception, string cacheKey);
}
