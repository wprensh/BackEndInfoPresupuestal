using System.Linq.Expressions;
using InfoPresupuestal.Api.Common.Abstractions;

namespace InfoPresupuestal.Api.Features.Administracion.Usuarios;

internal static class UsuariosApi
{
    public const string Route = $"{AdministracionApi.Route}/usuarios";

    public static Error NotFound(Guid id) => Error.NotFound("Usuarios.NotFound", $"No existe un usuario con id '{id}'.");

    public static Error RolInvalido(Guid rolId) => Error.Validation("Usuarios.RolInvalido", $"No existe el rol '{rolId}' en la empresa.");
}

public sealed record UsuarioResponse(
    Guid Id,
    string Email,
    string Nombre,
    Guid RolId,
    string Rol,
    bool Activo,
    bool EsSuperAdmin,
    DateTimeOffset? UltimoAcceso,
    DateTimeOffset CreadoEn);

internal static class UsuarioMapping
{
    /// <summary>Proyección traducible a SQL; nunca expone el hash de la contraseña.</summary>
    public static readonly Expression<Func<Usuario, UsuarioResponse>> ToResponse = u =>
        new UsuarioResponse(u.Id, u.Email, u.Nombre, u.RolId, u.Rol!.Nombre, u.Activo, u.EsSuperAdmin, u.UltimoAcceso, u.CreadoEn);
}
