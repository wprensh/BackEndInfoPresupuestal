using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Features.Products;
using InfoPresupuestal.Api.Features.Products.GetProductById;

namespace InfoPresupuestal.Api.Tests.Features.Products;

public sealed class GetProductByIdHandlerTests
{
    [Fact]
    public async Task Returns_product_when_it_exists()
    {
        await using var db = TestDb.Create();
        var product = Product.Create("Mouse", null, 25m, 3, DateTimeOffset.UtcNow);
        db.Add(product);
        await db.SaveChangesAsync();

        var result = await new GetProductByIdHandler(db).HandleAsync(new GetProductByIdQuery(product.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Mouse", result.Value.Name);
    }

    [Fact]
    public async Task Returns_not_found_when_missing()
    {
        await using var db = TestDb.Create();

        var result = await new GetProductByIdHandler(db).HandleAsync(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}
