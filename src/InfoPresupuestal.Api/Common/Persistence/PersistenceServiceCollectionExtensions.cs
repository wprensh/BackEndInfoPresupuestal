using InfoPresupuestal.Api.Common.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Common.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'Postgres'.");

        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
            .AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>()));

        return services;
    }
}
