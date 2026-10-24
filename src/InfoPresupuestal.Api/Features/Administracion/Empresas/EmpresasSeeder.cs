using InfoPresupuestal.Api.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InfoPresupuestal.Api.Features.Administracion.Empresas;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; }

    /// <summary>Contraseña inicial de los usuarios sembrados. Solo para entornos locales.</summary>
    public string Password { get; set; } = string.Empty;

    public string SuperAdminEmail { get; set; } = "superadmin@infopresupuestal.local";

    public IList<SeedEmpresa> Empresas { get; init; } = [];
}

public sealed class SeedEmpresa
{
    public string TenantId { get; set; } = string.Empty;

    public string RazonSocial { get; set; } = string.Empty;

    public string AdminEmail { get; set; } = string.Empty;
}

/// <summary>
/// Si la tabla de empresas está vacía y <c>Seed:Enabled</c> es true, crea la empresa de plataforma con el
/// superadministrador y las empresas de ejemplo configuradas, cada una con su administrador.
/// </summary>
internal sealed partial class EmpresasSeeder(
    AppDbContext db,
    EmpresaProvisioner provisioner,
    TimeProvider timeProvider,
    IOptions<SeedOptions> options,
    ILogger<EmpresasSeeder> logger) : IDataSeeder
{
    public int Order => 10;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var seed = options.Value;
        if (!seed.Enabled || await db.Set<Empresa>().AnyAsync(cancellationToken))
        {
            return;
        }

        if (seed.Password.Length < 8)
        {
            throw new InvalidOperationException("Seed:Password debe tener al menos 8 caracteres.");
        }

        var now = timeProvider.GetUtcNow();
        var plataforma = Empresa.Create(Empresa.TenantPlataforma, "InfoPresupuestal (plataforma)", null, null, null, now);
        await provisioner.ProvisionAsync(plataforma, "Superadministrador", seed.SuperAdminEmail, seed.Password, cancellationToken, adminEsSuperAdmin: true);

        foreach (var item in seed.Empresas)
        {
            var empresa = Empresa.Create(item.TenantId, item.RazonSocial, null, null, null, now);
            await provisioner.ProvisionAsync(empresa, $"Administrador {item.RazonSocial}", item.AdminEmail, seed.Password, cancellationToken);
        }

        LogSeeded(logger, seed.Empresas.Count + 1);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Siembra inicial: {Count} empresas creadas")]
    private static partial void LogSeeded(ILogger logger, int count);
}
