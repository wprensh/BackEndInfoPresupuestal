namespace InfoPresupuestal.Api.Common.Abstractions;

/// <summary>Operación que modifica estado y devuelve <typeparamref name="TResponse"/>.</summary>
public interface ICommand<TResponse>;

/// <summary>Operación de solo lectura que devuelve <typeparamref name="TResponse"/>.</summary>
public interface IQuery<TResponse>;

/// <summary>
/// Consulta cuyo resultado se guarda en caché. El handler no sabe nada de la caché:
/// la aplica <see cref="Caching.CachedQueryHandler{TQuery, TResponse}"/>.
/// </summary>
public interface ICachedQuery<TResponse> : IQuery<TResponse>
{
    string CacheKey { get; }

    TimeSpan? Expiration { get; }
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
