using System.Security.Claims;

namespace InfoPresupuestal.Api.Common.Security;

/// <summary>Única regla de autorización por permisos; la usan las políticas y <see cref="ICurrentUser"/>.</summary>
public static class PermissionEvaluator
{
    public static bool Allows(ClaimsPrincipal user, string modulo, Accion accion)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.HasClaim(AppClaims.SuperAdmin, "true"))
        {
            return true;
        }

        var definicion = Modulos.Buscar(modulo);
        return definicion is not null
            && definicion.PermiteEnRol(accion)
            && user.HasClaim(AppClaims.Permission, Modulos.Permiso(modulo, accion));
    }
}
