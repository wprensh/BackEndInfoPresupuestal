using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;
using Microsoft.AspNetCore.Identity;

namespace InfoPresupuestal.Api.Features.Administracion;

internal sealed class AdministracionInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddScoped<ITenantStore, EmpresaTenantStore>();
        services.AddScoped<EmpresaProvisioner>();
        services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);
    }
}
