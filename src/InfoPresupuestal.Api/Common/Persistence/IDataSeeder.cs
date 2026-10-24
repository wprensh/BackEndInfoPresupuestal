using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfoPresupuestal.Api.Common.Persistence;

/// <summary>Carga datos iniciales. Se ejecuta al arrancar en Development, después de las migraciones.</summary>
public interface IDataSeeder
{
    /// <summary>Orden de ejecución: los menores primero.</summary>
    int Order { get; }

    Task SeedAsync(CancellationToken cancellationToken);
}

public static class DataSeederExtensions
{
    public static IServiceCollection AddDataSeeders(this IServiceCollection services, Assembly assembly)
    {
        var descriptors = assembly.DefinedTypes
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IDataSeeder)))
            .Select(t => ServiceDescriptor.Scoped(typeof(IDataSeeder), t));

        services.TryAddEnumerable(descriptors);
        return services;
    }

    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        foreach (var seeder in scope.ServiceProvider.GetServices<IDataSeeder>().OrderBy(s => s.Order))
        {
            await seeder.SeedAsync(app.Lifetime.ApplicationStopping);
        }
    }
}
