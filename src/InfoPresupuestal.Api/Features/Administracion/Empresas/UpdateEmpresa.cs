using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace InfoPresupuestal.Api.Features.Administracion.Empresas;

/// <summary>El identificador (tenant) no se cambia: está en todas las filas de la empresa.</summary>
public sealed record UpdateEmpresaRequest(string RazonSocial, string? Nit, string? Email, string? Telefono, bool Activo);

public sealed record UpdateEmpresaCommand(Guid Id, UpdateEmpresaRequest Datos) : ICommand<EmpresaResponse>;

internal sealed class UpdateEmpresaValidator : AbstractValidator<UpdateEmpresaRequest>
{
    public UpdateEmpresaValidator()
    {
        RuleFor(c => c.RazonSocial).NotEmpty().MaximumLength(EmpresaConfiguration.RazonSocialMaxLength);
        RuleFor(c => c.Nit).MaximumLength(20);
        RuleFor(c => c.Email).EmailAddress().MaximumLength(200).When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.Telefono).MaximumLength(30);
    }
}

internal sealed class UpdateEmpresaHandler(AppDbContext db, IMemoryCache cache) : ICommandHandler<UpdateEmpresaCommand, EmpresaResponse>
{
    public async Task<Result<EmpresaResponse>> HandleAsync(UpdateEmpresaCommand command, CancellationToken cancellationToken)
    {
        var empresa = await db.Set<Empresa>().FirstOrDefaultAsync(e => e.Id == command.Id, cancellationToken);
        if (empresa is null)
        {
            return EmpresasApi.NotFound(command.Id);
        }

        var d = command.Datos;
        empresa.Update(d.RazonSocial, d.Nit, d.Email, d.Telefono, d.Activo);
        await db.SaveChangesAsync(cancellationToken);
        EmpresaTenantStore.Invalidate(cache, empresa.TenantId);

        return empresa.AsResponse();
    }
}

internal sealed class UpdateEmpresaEndpoint : IPlatformEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut($"{EmpresasApi.Route}/{{id:guid}}", async (Guid id, UpdateEmpresaRequest datos, ICommandHandler<UpdateEmpresaCommand, EmpresaResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdateEmpresaCommand(id, datos), ct)).ToOk())
            .WithName("UpdateEmpresa")
            .WithTags(AdministracionApi.Tag)
            .WithRequestValidation<UpdateEmpresaRequest>()
            .Produces<EmpresaResponse>()
            .RequireAuthorization(PermissionEndpointExtensions.SuperAdminPolicy);
}
