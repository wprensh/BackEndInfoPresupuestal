using System.Security.Claims;
using System.Text.RegularExpressions;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.Extensions.Options;

namespace InfoPresupuestal.Api.Common.MultiTenancy;

/// <summary>
/// Resuelve el tenant antes de ejecutar el endpoint:
/// <list type="bullet">
/// <item>usuario normal: el claim <c>tenant</c> de su JWT (no puede elegir otro);</item>
/// <item>superadministrador: el header <c>X-Tenant-Id</c> si lo envía, si no su propio tenant.</item>
/// </list>
/// Sin tenant válido y activo la solicitud se rechaza: ningún handler corre fuera de un tenant.
/// </summary>
internal sealed partial class TenantResolutionFilter(
    TenantContext tenantContext,
    ITenantStore tenantStore,
    IOptions<MultiTenancyOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var user = http.User;

        var tenantId = user.FindFirstValue(AppClaims.Tenant) ?? string.Empty;
        if (user.HasClaim(AppClaims.SuperAdmin, "true"))
        {
            var requested = http.Request.Headers[options.Value.HeaderName].ToString().Trim();
            if (requested.Length > 0)
            {
                tenantId = requested;
            }
        }

        if (tenantId.Length == 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Tenant.Missing",
                detail: "El token no indica la empresa del usuario.");
        }

        if (!TenantIdFormat().IsMatch(tenantId) || !await tenantStore.ExistsAsync(tenantId, http.RequestAborted))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Tenant.Inactive",
                detail: "La empresa no existe o está inactiva.");
        }

        tenantContext.SetTenant(tenantId);
        return await next(context);
    }

    // Solo minúsculas, dígitos y guiones: el id se usa en claves de caché y en columnas.
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}$")]
    internal static partial Regex TenantIdFormat();
}
