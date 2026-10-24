using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;

namespace InfoPresupuestal.Api.Features.Presupuestos.Apus;

public sealed record GetApuQuery(Guid Id) : IQuery<ApuResponse>;

internal sealed class GetApuHandler(AppDbContext db) : IQueryHandler<GetApuQuery, ApuResponse>
{
    public async Task<Result<ApuResponse>> HandleAsync(GetApuQuery query, CancellationToken cancellationToken) =>
        await ApusApi.LeerAsync(db, query.Id, cancellationToken) is { } apu ? apu : ApusApi.NotFound(query.Id);
}

internal sealed class GetApuEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{ApusApi.Route}/{{id:guid}}", async (Guid id, IQueryHandler<GetApuQuery, ApuResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetApuQuery(id), ct)).ToOk())
            .WithName("GetApu")
            .WithTags(PresupuestosModulo.Tag)
            .Produces<ApuResponse>()
            .RequirePermission(Modulos.Apu, Accion.Ver);
}
