using System.Reflection;
using InfoPresupuestal.Api.Common.MultiTenancy;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfoPresupuestal.Api.Common.Endpoints;

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var descriptors = assembly.DefinedTypes
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IEndpoint)))
            .Select(t => ServiceDescriptor.Transient(typeof(IEndpoint), t));

        services.TryAddEnumerable(descriptors);
        return services;
    }

    /// <summary>
    /// Mapea los slices en tres grupos:
    /// públicos (sin autenticación), de plataforma (autenticados, sin tenant) y el resto
    /// (autenticados y con tenant resuelto). Un endpoint nuevo queda protegido por defecto.
    /// </summary>
    public static IApplicationBuilder MapEndpoints(this WebApplication app)
    {
        var publicGroup = app.MapGroup(string.Empty).AllowAnonymous();
        var platformGroup = app.MapGroup(string.Empty).RequireAuthorization();
        var tenantGroup = app.MapGroup(string.Empty).RequireAuthorization().RequireTenant();

        foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            var group = endpoint switch
            {
                IPublicEndpoint => publicGroup,
                IPlatformEndpoint => platformGroup,
                _ => tenantGroup,
            };
            endpoint.MapEndpoint(group);
        }

        return app;
    }
}
