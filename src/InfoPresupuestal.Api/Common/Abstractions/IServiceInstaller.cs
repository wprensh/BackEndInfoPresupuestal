using System.Reflection;

namespace InfoPresupuestal.Api.Common.Abstractions;

/// <summary>
/// Servicios propios de una feature (por ejemplo la implementación de <see cref="MultiTenancy.ITenantStore"/>).
/// Se descubren por reflexión, así <c>Program.cs</c> no cambia al agregar una feature.
/// </summary>
public interface IServiceInstaller
{
    void Install(IServiceCollection services, IConfiguration configuration);
}

public static class ServiceInstallerExtensions
{
    public static IServiceCollection AddServiceInstallers(this IServiceCollection services, IConfiguration configuration, Assembly assembly)
    {
        var installers = assembly.DefinedTypes
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IServiceInstaller)))
            .Select(t => (IServiceInstaller)Activator.CreateInstance(t)!);

        foreach (var installer in installers)
        {
            installer.Install(services, configuration);
        }

        return services;
    }
}
