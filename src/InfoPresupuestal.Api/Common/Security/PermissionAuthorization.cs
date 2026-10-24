using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace InfoPresupuestal.Api.Common.Security;

internal sealed record PermissionRequirement(string Modulo, Accion Accion) : IAuthorizationRequirement;

internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (PermissionEvaluator.Allows(context.User, requirement.Modulo, requirement.Accion))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Crea al vuelo las políticas <c>perm:modulo:accion</c>, así no hay que registrar una por permiso.
/// </summary>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "perm:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return await base.GetPolicyAsync(policyName);
        }

        var parts = policyName[Prefix.Length..].Split(':');
        if (parts.Length != 2 || !Enum.TryParse<Accion>(parts[1], ignoreCase: true, out var accion))
        {
            return null;
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(parts[0], accion))
            .Build();
    }
}

public static class PermissionEndpointExtensions
{
    public const string SuperAdminPolicy = "superadmin";

    /// <summary>Exige el permiso <paramref name="accion"/> sobre <paramref name="modulo"/>.</summary>
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string modulo, Accion accion) =>
        builder
            .RequireAuthorization($"{PermissionPolicyProvider.Prefix}{modulo}:{accion.ToString().ToLowerInvariant()}")
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
