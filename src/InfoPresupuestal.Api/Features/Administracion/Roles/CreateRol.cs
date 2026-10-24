using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Roles;

public sealed record CreateRolCommand(string Nombre, string? Descripcion, IReadOnlyList<PermisoDto> Permisos) : ICommand<RolResponse>;

internal sealed class CreateRolValidator : AbstractValidator<CreateRolCommand>
{
    public CreateRolValidator()
    {
        RuleFor(c => c.Nombre).NotEmpty().MaximumLength(RolConfiguration.NombreMaxLength);
        RuleFor(c => c.Descripcion).MaximumLength(RolConfiguration.DescripcionMaxLength);
        RuleFor(c => c.Permisos).NotNull().SetValidator(new PermisosValidator());
    }
}

internal sealed class CreateRolHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<CreateRolCommand, RolResponse>
{
    public async Task<Result<RolResponse>> HandleAsync(CreateRolCommand command, CancellationToken cancellationToken)
    {
        var nombre = command.Nombre.Trim();
        if (await db.Set<Rol>().AnyAsync(r => EF.Functions.ILike(r.Nombre, LikePattern.Exact(nombre)), cancellationToken))
        {
            return RolesApi.NombreDuplicado(nombre);
        }

        var rol = Rol.Create(nombre, command.Descripcion, command.Permisos.ToEntities(), timeProvider.GetUtcNow());
        db.Add(rol);
        await db.SaveChangesAsync(cancellationToken);
        return rol.AsResponse();
    }
}

internal sealed class CreateRolEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(RolesApi.Route, async (CreateRolCommand command, ICommandHandler<CreateRolCommand, RolResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(command, ct)).ToHttp(r => Results.Created($"{RolesApi.Route}/{r.Id}", r)))
            .WithName("CreateRol")
            .WithTags(AdministracionApi.Tag)
            .WithRequestValidation<CreateRolCommand>()
            .Produces<RolResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequirePermission(Modulos.Roles, Accion.Insertar);
}
