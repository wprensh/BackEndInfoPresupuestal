using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Features.Products;
using InfoPresupuestal.Api.Features.Products.CreateProduct;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Tests.Features.Products;

public sealed class CreateProductHandlerTests
{
    [Fact]
    public async Task Persists_product_and_returns_response()
    {
        await using var db = TestDb.Create();
        var handler = new CreateProductHandler(db, TimeProvider.System);

        var result = await handler.HandleAsync(new CreateProductCommand(" Laptop ", "14\"", 1299.90m, 10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Laptop", result.Value.Name);
        var saved = await db.Set<Product>().SingleAsync();
        Assert.Equal(result.Value.Id, saved.Id);
    }

    [Fact]
    public async Task Rejects_duplicate_name()
    {
        await using var db = TestDb.Create();
        var handler = new CreateProductHandler(db, TimeProvider.System);
        await handler.HandleAsync(new CreateProductCommand("Laptop", null, 10m, 1), CancellationToken.None);

        var result = await handler.HandleAsync(new CreateProductCommand("Laptop", null, 20m, 2), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Theory]
    [InlineData("", 10, 1)]
    [InlineData("Laptop", -1, 1)]
    [InlineData("Laptop", 10, -1)]
    public void Validator_rejects_invalid_commands(string name, decimal price, int stock)
    {
        var validation = new CreateProductValidator().Validate(new CreateProductCommand(name, null, price, stock));

        Assert.False(validation.IsValid);
    }
}
