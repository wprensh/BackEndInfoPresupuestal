using System.Security.Claims;

namespace InfoPresupuestal.Api.Common.Security;

/// <summary>Usuario autenticado de la solicitud, leído del JWT.</summary>
public interface ICurrentUser
{
    Guid? Id { get; }

    bool IsSuperAdmin { get; }

    bool Can(string modulo, Accion accion);
}

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? Id => Guid.TryParse(Principal?.FindFirstValue(AppClaims.UserId), out var id) ? id : null;

    public bool IsSuperAdmin => Principal?.HasClaim(AppClaims.SuperAdmin, "true") == true;

    public bool Can(string modulo, Accion accion) => Principal is not null && PermissionEvaluator.Allows(Principal, modulo, accion);
}
