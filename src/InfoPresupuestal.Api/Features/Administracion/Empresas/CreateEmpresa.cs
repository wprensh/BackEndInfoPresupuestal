using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Empresas;

/// <summary>Crea la empresa (tenant), constructora o proveedor, con sus roles base y su primer administrador.</summary>
public sealed record CreateEmpresaCommand(
    string TenantId,
    string RazonSocial,
    string? Nit,
    string? Email,
    string? Telefono,
    string AdminNombre,
    string AdminEmail,
    string AdminPassword,
    TipoEmpresa Tipo = TipoEmpresa.Constructora) : ICommand<EmpresaResponse>;

internal sealed class CreateEmpresaValidator : AbstractValidator<CreateEmpresaCommand>
{
    public CreateEmpresaValidator()
    {
        RuleFor(c => c.TenantId).TenantId().NotEqual(Empresa.TenantPlataforma).WithMessage("Ese identificador está reservado.");
        RuleFor(c => c.RazonSocial).NotEmpty().MaximumLength(EmpresaConfiguration.RazonSocialMaxLength);
        RuleFor(c => c.Nit).MaximumLength(20);
        RuleFor(c => c.Email).EmailAddress().MaximumLength(200).When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.Telefono).MaximumLength(30);
        RuleFor(c => c.AdminNombre).NotEmpty().MaximumLength(UsuarioConfiguration.NombreMaxLength);
        RuleFor(c => c.AdminEmail).NotEmpty().EmailAddress().MaximumLength(UsuarioConfiguration.EmailMaxLength);
        RuleFor(c => c.AdminPassword).ContrasenaSegura();
        RuleFor(c => c.Tipo).IsInEnum();
    }
}

internal sealed class CreateEmpresaHandler(AppDbContext db, EmpresaProvisioner provisioner, TimeProvider timeProvider)
    : ICommandHandler<CreateEmpresaCommand, EmpresaResponse>
{
    public async Task<Result<EmpresaResponse>> HandleAsync(CreateEmpresaCommand command, CancellationToken cancellationToken)
    {
        var tenantId = command.TenantId.Trim().ToLowerInvariant();
        if (await db.Set<Empresa>().AnyAsync(e => e.TenantId == tenantId, cancellationToken))
        {
            return Error.Conflict("Empresas.TenantExists", $"Ya existe una empresa con el identificador '{tenantId}'.");
        }

        var empresa = Empresa.Create(tenantId, command.RazonSocial, command.Nit, command.Email, command.Telefono, timeProvider.GetUtcNow(), command.Tipo);
        await provisioner.ProvisionAsync(empresa, command.AdminNombre, command.AdminEmail, command.AdminPassword, cancellationToken);
        return empresa.AsResponse();
    }
}

internal sealed class CreateEmpresaEndpoint : IPlatformEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(EmpresasApi.Route, async (CreateEmpresaCommand command, ICommandHandler<CreateEmpresaCommand, EmpresaResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(command, ct)).ToHttp(e => Results.Created($"{EmpresasApi.Route}/{e.Id}", e)))
            .WithName("CreateEmpresa")
            .WithTags(AdministracionApi.Tag)
            .WithRequestValidation<CreateEmpresaCommand>()
            .Produces<EmpresaResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(PermissionEndpointExtensions.SuperAdminPolicy);
}
