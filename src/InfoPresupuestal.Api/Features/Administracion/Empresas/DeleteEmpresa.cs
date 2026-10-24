using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace InfoPresupuestal.Api.Features.Administracion.Empresas;

/// <summary>
/// Eliminar una empresa la desactiva: sus usuarios dejan de poder entrar, pero sus datos se conservan.
/// Borrar físicamente un tenant es una operación de base de datos, no de la API.
/// </summary>
public sealed record DeleteEmpresaCommand(Guid Id) : ICommand<Done>;

internal sealed class DeleteEmpresaHandler(AppDbContext db, IMemoryCache cache) : ICommandHandler<DeleteEmpresaCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(DeleteEmpresaCommand command, CancellationToken cancellationToken)
    {
        var empresa = await db.Set<Empresa>().FirstOrDefaultAsync(e => e.Id == command.Id, cancellationToken);
        if (empresa is null)
        {
            return EmpresasApi.NotFound(command.Id);
        }

        if (empresa.EsPlataforma)
        {
            return Error.Conflict("Empresas.Plataforma", "La empresa de plataforma no se puede desactivar.");
        }

        empresa.Desactivar();
        await db.SaveChangesAsync(cancellationToken);
        EmpresaTenantStore.Invalidate(cache, empresa.TenantId);
        return new Done();
    }
}

internal sealed class DeleteEmpresaEndpoint : IPlatformEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete($"{EmpresasApi.Route}/{{id:guid}}", async (Guid id, ICommandHandler<DeleteEmpresaCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DeleteEmpresaCommand(id), ct)).ToNoContent())
            .WithName("DeleteEmpresa")
            .WithTags(AdministracionApi.Tag)
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization(PermissionEndpointExtensions.SuperAdminPolicy);
}
