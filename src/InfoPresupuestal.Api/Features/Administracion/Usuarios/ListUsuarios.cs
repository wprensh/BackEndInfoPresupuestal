using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Usuarios;

public sealed record ListUsuariosRequest(int Page = 1, int PageSize = 20, string? Search = null, Guid? RolId = null, bool? Activo = null)
    : PageRequest(Page, PageSize, Search);

public sealed record ListUsuariosQuery(ListUsuariosRequest Filtro) : IQuery<PagedResponse<UsuarioResponse>>;

internal sealed class ListUsuariosHandler(AppDbContext db) : IQueryHandler<ListUsuariosQuery, PagedResponse<UsuarioResponse>>
{
    public async Task<Result<PagedResponse<UsuarioResponse>>> HandleAsync(ListUsuariosQuery query, CancellationToken cancellationToken)
    {
        var f = query.Filtro;
        var usuarios = db.Set<Usuario>().AsNoTracking();
        if (f.SearchPattern is { } pattern)
        {
            usuarios = usuarios.Where(u => EF.Functions.ILike(u.Nombre, pattern) || EF.Functions.ILike(u.Email, pattern));
        }

        if (f.RolId is { } rolId)
        {
            usuarios = usuarios.Where(u => u.RolId == rolId);
        }

        if (f.Activo is { } activo)
        {
            usuarios = usuarios.Where(u => u.Activo == activo);
        }

        return await usuarios.OrderBy(u => u.Nombre).Select(UsuarioMapping.ToResponse).ToPagedAsync(f, cancellationToken);
    }
}

internal sealed class ListUsuariosEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(UsuariosApi.Route, async ([AsParameters] ListUsuariosRequest filtro, IQueryHandler<ListUsuariosQuery, PagedResponse<UsuarioResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListUsuariosQuery(filtro), ct)).ToOk())
            .WithName("ListUsuarios")
            .WithTags(AdministracionApi.Tag)
            .RequirePermission(Modulos.Usuarios, Accion.Ver);
}
