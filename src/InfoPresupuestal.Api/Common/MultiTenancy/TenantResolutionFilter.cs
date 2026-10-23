using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace InfoPresupuestal.Api.Common.MultiTenancy;

/// <summary>
/// Resuelve el tenant desde el header antes de ejecutar el endpoint. Sin tenant válido la solicitud
/// se rechaza: ningún handler corre fuera de un tenant.
/// </summary>
internal sealed partial class TenantResolutionFilter(
    TenantContext tenantContext,
    ITenantStore tenantStore,
    IOptions<MultiTenancyOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var headerName = options.Value.HeaderName;
        var tenantId = context.HttpContext.Request.Headers[headerName].ToString().Trim();

        if (tenantId.Length == 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Tenant.Missing",
                detail: $"El header '{headerName}' es obligatorio.");
        }

        if (!TenantIdFormat().IsMatch(tenantId) ||
            !await tenantStore.ExistsAsync(tenantId, context.HttpContext.RequestAborted))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Tenant.NotFound",
                detail: "El tenant indicado no existe.");
        }

        tenantContext.SetTenant(tenantId);
        return await next(context);
    }

    // Solo minúsculas, dígitos y guiones: el id se usa en claves de caché y en columnas.
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}$")]
    private static partial Regex TenantIdFormat();
}
