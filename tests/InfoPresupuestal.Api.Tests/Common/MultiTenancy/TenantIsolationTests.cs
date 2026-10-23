using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Features.Products;
using InfoPresupuestal.Api.Features.Products.CreateProduct;
using InfoPresupuestal.Api.Features.Products.GetProductById;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Tests.Common.MultiTenancy;

public sealed class TenantIsolationTests
{
    private readonly string _database = Guid.NewGuid().ToString();

    [Fact]
    public async Task New_products_get_the_current_tenant()
    {
        await using var db = TestDb.Create("acme", _database);

        await new CreateProductHandler(db, TimeProvider.System)
            .HandleAsync(new CreateProductCommand("Laptop", null, 10m, 1), CancellationToken.None);

        Assert.Equal("acme", (await db.Set<Product>().SingleAsync()).TenantId);
    }

    [Fact]
    public async Task A_tenant_cannot_read_another_tenants_product()
    {
        Guid id;
        await using (var acme = TestDb.Create("acme", _database))
        {
            var created = await new CreateProductHandler(acme, TimeProvider.System)
                .HandleAsync(new CreateProductCommand("Laptop", null, 10m, 1), CancellationToken.None);
            id = created.Value.Id;
        }

        await using var demo = TestDb.Create("demo", _database);
        var result = await new GetProductByIdHandler(demo).HandleAsync(new GetProductByIdQuery(id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Empty(await demo.Set<Product>().ToListAsync());
    }

    [Fact]
    public async Task Product_names_are_unique_per_tenant_only()
    {
        await using (var acme = TestDb.Create("acme", _database))
        {
            await new CreateProductHandler(acme, TimeProvider.System)
                .HandleAsync(new CreateProductCommand("Laptop", null, 10m, 1), CancellationToken.None);
        }

        await using var demo = TestDb.Create("demo", _database);
        var result = await new CreateProductHandler(demo, TimeProvider.System)
            .HandleAsync(new CreateProductCommand("Laptop", null, 20m, 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Changing_another_tenants_row_is_rejected()
    {
        await using (var acme = TestDb.Create("acme", _database))
        {
            acme.Add(Product.Create("Laptop", null, 10m, 1, DateTimeOffset.UtcNow));
            await acme.SaveChangesAsync();
        }

        await using var demo = TestDb.Create("demo", _database);
        var foreign = await demo.Set<Product>().IgnoreQueryFilters().SingleAsync();
        demo.Remove(foreign);

        await Assert.ThrowsAsync<InvalidOperationException>(() => demo.SaveChangesAsync());
    }
}
