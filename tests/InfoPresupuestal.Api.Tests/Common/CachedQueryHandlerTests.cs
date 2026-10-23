using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Caching;
using InfoPresupuestal.Api.Features.Products.GetProductById;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfoPresupuestal.Api.Tests.Common;

public sealed class CachedQueryHandlerTests
{
    private readonly ICacheService _cache =
        new DistributedCacheService(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    [Fact]
    public async Task Second_call_is_served_from_cache()
    {
        var inner = new CountingHandler(id => new GetProductByIdResponse(id, "Teclado", null, 50m, 1, DateTimeOffset.UtcNow));
        var handler = CreateSut(inner);
        var query = new GetProductByIdQuery(Guid.NewGuid());

        var first = await handler.HandleAsync(query, CancellationToken.None);
        var second = await handler.HandleAsync(query, CancellationToken.None);

        Assert.Equal(1, inner.Calls);
        Assert.Equal(first.Value, second.Value);
    }

    [Fact]
    public async Task Failures_are_not_cached()
    {
        var inner = new CountingHandler(_ => Error.NotFound("x", "y"));
        var handler = CreateSut(inner);
        var query = new GetProductByIdQuery(Guid.NewGuid());

        await handler.HandleAsync(query, CancellationToken.None);
        await handler.HandleAsync(query, CancellationToken.None);

        Assert.Equal(2, inner.Calls);
    }

    private CachedQueryHandler<GetProductByIdQuery, GetProductByIdResponse> CreateSut(CountingHandler inner) =>
        new(inner, _cache, NullLogger<CachedQueryHandler<GetProductByIdQuery, GetProductByIdResponse>>.Instance);

    private sealed class CountingHandler(Func<Guid, Result<GetProductByIdResponse>> respond)
        : IQueryHandler<GetProductByIdQuery, GetProductByIdResponse>
    {
        public int Calls { get; private set; }

        public Task<Result<GetProductByIdResponse>> HandleAsync(GetProductByIdQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond(query.Id));
        }
    }
}
