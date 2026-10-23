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

    /// <summary>Mapea todos los slices en un grupo que exige tenant.</summary>
    public static IApplicationBuilder MapEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(string.Empty).RequireTenant();

        foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.MapEndpoint(group);
        }

        return app;
    }
}
