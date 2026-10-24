using System.Linq.Expressions;
using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Security;

namespace InfoPresupuestal.Api.Features.Administracion.Roles;

internal static class RolesApi
{
    public const string Route = $"{AdministracionApi.Route}/roles";

    public static Error NotFound(Guid id) => Error.NotFound("Roles.NotFound", $"No existe un rol con id '{id}'.");

    public static Error NombreDuplicado(string nombre) => Error.Conflict("Roles.NombreDuplicado", $"Ya existe un rol llamado '{nombre}'.");

    public static Error EsSistema() => Error.Conflict("Roles.Sistema", "Los roles Administrador y Consulta no se modifican ni se eliminan.");
}

/// <summary>Una fila de la matriz de permisos: un módulo y las cuatro acciones.</summary>
public sealed record PermisoDto(string Modulo, bool Ver, bool Insertar, bool Editar, bool Eliminar);

public sealed record RolResponse(Guid Id, string Nombre, string? Descripcion, bool EsSistema, IReadOnlyList<PermisoDto> Permisos);

public sealed record RolResumenResponse(Guid Id, string Nombre, string? Descripcion, bool EsSistema, int Usuarios);

internal static class RolMapping
{
    public static RolResponse AsResponse(this Rol rol) =>
        new(rol.Id, rol.Nombre, rol.Descripcion, rol.EsSistema,
            rol.Permisos.OrderBy(p => p.Modulo).Select(p => new PermisoDto(p.Modulo, p.Ver, p.Insertar, p.Editar, p.Eliminar)).ToList());

    public static IEnumerable<RolPermiso> ToEntities(this IEnumerable<PermisoDto> permisos) =>
        permisos
            .Where(p => p.Ver || p.Insertar || p.Editar || p.Eliminar)
            .Select(p => RolPermiso.Create(p.Modulo, p.Ver, p.Insertar, p.Editar, p.Eliminar));
}

/// <summary>Reglas de la matriz de permisos, compartidas por crear y editar rol.</summary>
internal sealed class PermisosValidator : AbstractValidator<IReadOnlyList<PermisoDto>>
{
    public PermisosValidator()
    {
        RuleFor(p => p)
            .Must(p => p.Select(x => x.Modulo).Distinct(StringComparer.Ordinal).Count() == p.Count)
            .WithMessage("Un módulo aparece más de una vez.");

        RuleForEach(p => p).ChildRules(permiso =>
        {
            permiso.RuleFor(x => x.Modulo)
                .Must(m => Modulos.Buscar(m) is { Alcance: not AlcanceModulo.Plataforma })
                .WithMessage(x => $"El módulo '{x.Modulo}' no existe o no se asigna a roles de empresa.");

            permiso.RuleFor(x => x)
                .Must(x => Modulos.Buscar(x.Modulo) is not { Alcance: AlcanceModulo.Global } || !(x.Insertar || x.Editar || x.Eliminar))
                .WithName("Permisos")
                .WithMessage(x => $"'{x.Modulo}' es un catálogo compartido: los roles de empresa solo pueden tener Ver.");
        });
    }
}
