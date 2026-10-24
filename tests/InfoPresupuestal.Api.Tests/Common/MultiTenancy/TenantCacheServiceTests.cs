using InfoPresupuestal.Api.Common.Caching;
using InfoPresupuestal.Api.Common.MultiTenancy;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace InfoPresupuestal.Api.Tests.Common.MultiTenancy;

public sealed class TenantCacheServiceTests
{
    private readonly DistributedCacheService _shared =
        new(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    [Fact]
    public async Task Same_key_is_isolated_between_tenants()
    {
        var acme = For("acme");
        var demo = For("demo");

        await acme.SetAsync("products:1", "de acme");

        Assert.Equal("de acme", await acme.GetAsync<string>("products:1"));
        Assert.Null(await demo.GetAsync<string>("products:1"));
        Assert.Equal("de acme", await _shared.GetAsync<string>("tenant:acme:products:1"));
    }

    private TenantCacheService For(string tenantId)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        return new TenantCacheService(_shared, tenant);
    }
}
