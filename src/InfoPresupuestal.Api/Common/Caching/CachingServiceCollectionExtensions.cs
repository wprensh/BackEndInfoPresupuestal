namespace InfoPresupuestal.Api.Common.Caching;

public static class CachingServiceCollectionExtensions
{
    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'Redis'.");

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = "infopresupuestal:";
        });

        services.AddSingleton<ICacheService, DistributedCacheService>();
        return services;
    }
}
