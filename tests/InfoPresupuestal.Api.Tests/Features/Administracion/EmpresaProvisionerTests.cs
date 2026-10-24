using InfoPresupuestal.Api.Features.Administracion.Roles;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Tests.Features.Administracion;

public sealed class EmpresaProvisionerTests
{
    private readonly string _database = Guid.NewGuid().ToString();

    [Fact]
    public async Task Each_company_gets_its_own_roles_and_admin()
    {
        await AdminTestData.ProvisionAsync(_database, ("acme", "admin@acme.co"), ("demo", "admin@demo.co"));

        await using var demo = TestDb.Create("demo", _database);
        var roles = await demo.Set<Rol>().OrderBy(r => r.Nombre).ToListAsync();
        var usuario = await demo.Set<Usuario>().SingleAsync();

        Assert.Equal([Rol.Administrador, Rol.Consulta], roles.Select(r => r.Nombre));
        Assert.All(roles, r => Assert.Equal("demo", r.TenantId));
        Assert.Equal("admin@demo.co", usuario.Email);
        Assert.Equal(roles[0].Id, usuario.RolId);
        Assert.NotEqual(AdminTestData.Password, usuario.PasswordHash);
    }

    [Fact]
    public void System_roles_never_include_platform_modules_or_write_global_catalogs()
    {
        var completos = Rol.PermisosCompletos().ToList();

        Assert.DoesNotContain(completos, p => p.Modulo == "empresas");
        Assert.All(completos.Where(p => p.Modulo is "unidades-medida" or "tipos-insumo"), p => Assert.False(p.Insertar || p.Editar || p.Eliminar));
        Assert.All(Rol.PermisosSoloLectura(), p => Assert.False(p.Insertar || p.Editar || p.Eliminar));
    }
}
