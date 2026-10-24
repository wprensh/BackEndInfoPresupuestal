using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;

namespace InfoPresupuestal.Api.Features.Presupuestos.Obras;

public sealed record GetPresupuestoQuery(Guid Id) : IQuery<PresupuestoResponse>;

internal sealed class GetPresupuestoHandler(AppDbContext db) : IQueryHandler<GetPresupuestoQuery, PresupuestoResponse>
{
    public async Task<Result<PresupuestoResponse>> HandleAsync(GetPresupuestoQuery query, CancellationToken cancellationToken) =>
        await PresupuestosApi.LeerAsync(db, query.Id, cancellationToken) is { } p ? p : PresupuestosApi.NotFound(query.Id);
}

internal sealed class GetPresupuestoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{PresupuestosApi.Route}/{{id:guid}}", async (Guid id, IQueryHandler<GetPresupuestoQuery, PresupuestoResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetPresupuestoQuery(id), ct)).ToOk())
            .WithName("GetPresupuesto")
            .WithTags(PresupuestosModulo.Tag)
            .Produces<PresupuestoResponse>()
            .RequirePermission(Modulos.Presupuestos, Accion.Ver);
}
