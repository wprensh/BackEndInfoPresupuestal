using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Security;

namespace InfoPresupuestal.Api.Features.Products.GetProductById;

internal sealed class GetProductByIdEndpoint : IEndpoint
{
    public const string Name = "GetProductById";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet($"{ProductsApi.Route}/{{id:guid}}", async (
                Guid id,
                IQueryHandler<GetProductByIdQuery, GetProductByIdResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new GetProductByIdQuery(id), cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : result.Error.ToProblem();
            })
            .WithName(Name)
            .WithTags(ProductsApi.Tag)
            .Produces<GetProductByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequirePermission(Modulos.Productos, Accion.Ver);
    }
}
