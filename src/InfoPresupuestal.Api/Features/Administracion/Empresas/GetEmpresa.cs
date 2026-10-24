using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Empresas;

public sealed record GetEmpresaQuery(Guid Id) : IQuery<EmpresaResponse>;

internal sealed class GetEmpresaHandler(AppDbContext db) : IQueryHandler<GetEmpresaQuery, EmpresaResponse>
{
    public async Task<Result<EmpresaResponse>> HandleAsync(GetEmpresaQuery query, CancellationToken cancellationToken)
    {
        var empresa = await db.Set<Empresa>().AsNoTracking()
            .Where(e => e.Id == query.Id)
            .Select(EmpresaMapping.ToResponse)
            .FirstOrDefaultAsync(cancellationToken);

        return empresa is null ? EmpresasApi.NotFound(query.Id) : empresa;
    }
}

internal sealed class GetEmpresaEndpoint : IPlatformEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{EmpresasApi.Route}/{{id:guid}}", async (Guid id, IQueryHandler<GetEmpresaQuery, EmpresaResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetEmpresaQuery(id), ct)).ToOk())
            .WithName("GetEmpresa")
            .WithTags(AdministracionApi.Tag)
            .Produces<EmpresaResponse>()
            .RequireAuthorization(PermissionEndpointExtensions.SuperAdminPolicy);
}
