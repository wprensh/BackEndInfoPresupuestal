using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Proveedores.Directorio;

/// <summary>
/// El administrador de una constructora da de alta la cuenta de un proveedor: una empresa de tipo
/// proveedor con su primer usuario administrador, que luego mantiene su propio catálogo.
/// </summary>
public sealed record CreateProveedorCommand(
    string TenantId,
    string RazonSocial,
    string? Nit,
    string? Email,
    string? Telefono,
    string AdminNombre,
    string AdminEmail,
    string AdminPassword) : ICommand<ProveedorResponse>;

internal sealed class CreateProveedorValidator : AbstractValidator<CreateProveedorCommand>
{
    public CreateProveedorValidator()
    {
        RuleFor(c => c.TenantId).TenantId().NotEqual(Empresa.TenantPlataforma).WithMessage("Ese identificador está reservado.");
        RuleFor(c => c.RazonSocial).NotEmpty().MaximumLength(EmpresaConfiguration.RazonSocialMaxLength);
        RuleFor(c => c.Nit).MaximumLength(20);
        RuleFor(c => c.Email).EmailAddress().MaximumLength(200).When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.Telefono).MaximumLength(30);
        RuleFor(c => c.AdminNombre).NotEmpty().MaximumLength(UsuarioConfiguration.NombreMaxLength);
        RuleFor(c => c.AdminEmail).NotEmpty().EmailAddress().MaximumLength(UsuarioConfiguration.EmailMaxLength);
        RuleFor(c => c.AdminPassword).ContrasenaSegura();
    }
}

internal sealed class CreateProveedorHandler(AppDbContext db, EmpresaProvisioner provisioner, TimeProvider timeProvider)
    : ICommandHandler<CreateProveedorCommand, ProveedorResponse>
{
    public async Task<Result<ProveedorResponse>> HandleAsync(CreateProveedorCommand command, CancellationToken cancellationToken)
    {
        var tenantId = command.TenantId.Trim().ToLowerInvariant();
        if (await db.Set<Empresa>().AnyAsync(e => e.TenantId == tenantId, cancellationToken))
        {
            return Error.Conflict("Empresas.TenantExists", $"Ya existe una empresa con el identificador '{tenantId}'.");
        }

        var empresa = Empresa.Create(tenantId, command.RazonSocial, command.Nit, command.Email, command.Telefono, timeProvider.GetUtcNow(), TipoEmpresa.Proveedor);
        await provisioner.ProvisionAsync(empresa, command.AdminNombre, command.AdminEmail, command.AdminPassword, cancellationToken);
        return new ProveedorResponse(empresa.Id, empresa.TenantId, empresa.RazonSocial, empresa.Nit, empresa.Email, empresa.Telefono, 0, empresa.CreadoEn);
    }
}

internal sealed class CreateProveedorEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(ProveedoresApi.DirectorioRoute, async (CreateProveedorCommand command, ICommandHandler<CreateProveedorCommand, ProveedorResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(command, ct)).ToHttp(p => Results.Created($"{ProveedoresApi.DirectorioRoute}/{p.Id}", p)))
            .WithName("CreateProveedor")
            .WithTags(ProveedoresApi.Tag)
            .WithRequestValidation<CreateProveedorCommand>()
            .Produces<ProveedorResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireTipoEmpresa(TipoEmpresa.Constructora)
            .RequirePermission(Modulos.Proveedores, Accion.Insertar);
}
