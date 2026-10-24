using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Products.GetProductById;

namespace InfoPresupuestal.Api.Features.Products.CreateProduct;

internal sealed class CreateProductEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(ProductsApi.Route, async (
                CreateProductCommand command,
                ICommandHandler<CreateProductCommand, CreateProductResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(command, cancellationToken);

                return result.IsSuccess
                    ? Results.CreatedAtRoute(GetProductByIdEndpoint.Name, new { id = result.Value.Id }, result.Value)
                    : result.Error.ToProblem();
            })
            .WithName("CreateProduct")
            .WithTags(ProductsApi.Tag)
            .WithRequestValidation<CreateProductCommand>()
            .Produces<CreateProductResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Productos, Accion.Insertar);
    }
}
