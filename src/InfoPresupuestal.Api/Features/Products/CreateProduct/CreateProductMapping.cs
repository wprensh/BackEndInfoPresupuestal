namespace InfoPresupuestal.Api.Features.Products.CreateProduct;

internal static class CreateProductMapping
{
    public static Product ToEntity(this CreateProductCommand command, DateTimeOffset createdAtUtc) =>
        Product.Create(command.Name, command.Description, command.Price, command.Stock, createdAtUtc);

    public static CreateProductResponse ToResponse(this Product product) =>
        new(product.Id, product.Name, product.Description, product.Price, product.Stock, product.CreatedAtUtc);
}
