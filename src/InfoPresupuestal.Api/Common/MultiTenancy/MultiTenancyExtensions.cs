namespace InfoPresupuestal.Api.Common.MultiTenancy;

public static class MultiTenancyExtensions
{
    public static IServiceCollection AddMultiTenancy(this IServiceCollection services)
    {
        services.AddOptions<MultiTenancyOptions>()
            .BindConfiguration(MultiTenancyOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddSingleton<ITenantStore, ConfigurationTenantStore>();
        services.AddScoped<TenantSaveChangesInterceptor>();

        return services;
    }

    /// <summary>Exige un tenant válido en todos los endpoints del grupo.</summary>
    public static TBuilder RequireTenant<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter<TBuilder, TenantResolutionFilter>();
        builder.WithMetadata(new RequiresTenantMetadata());
        return builder;
    }
}
