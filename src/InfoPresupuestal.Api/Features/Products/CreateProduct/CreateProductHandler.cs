using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Products.CreateProduct;

internal sealed class CreateProductHandler(AppDbContext db, TimeProvider timeProvider)
    : ICommandHandler<CreateProductCommand, CreateProductResponse>
{
    public async Task<Result<CreateProductResponse>> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        if (await db.Set<Product>().AnyAsync(p => p.Name == name, cancellationToken))
        {
            return ProductErrors.NameAlreadyExists(name);
        }

        var product = command.ToEntity(timeProvider.GetUtcNow());

        db.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return product.ToResponse();
    }
}
