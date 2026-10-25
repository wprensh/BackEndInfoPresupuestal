using InfoPresupuestal.Api.Features.Administracion.Roles;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

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

        Assert.Equal([Rol.Administrador, Rol.Consulta, Rol.Proveedor], roles.Select(r => r.Nombre));
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

    [Fact]
    public void Proveedor_only_maintains_supplies_and_prices_without_deleting()
    {
        var permisos = Rol.PermisosProveedor().ToDictionary(p => p.Modulo);

        Assert.Equal(["clasificaciones", "insumos", "precios-insumo", "tipos-insumo", "unidades-medida"], permisos.Keys.Order());
        Assert.True(permisos["insumos"] is { Insertar: true, Editar: true, Eliminar: false });
        Assert.True(permisos["precios-insumo"] is { Insertar: true, Editar: true, Eliminar: false });
        Assert.True(permisos["clasificaciones"] is { Insertar: true, Editar: false, Eliminar: false });
        Assert.All(permisos.Values, p => Assert.False(p.Eliminar));
    }

    [Fact]
    public async Task Seeder_adds_proveedor_role_to_existing_companies_once()
    {
        await AdminTestData.ProvisionAsync(_database, ("acme", "admin@acme.co"));
        await using (var acme = TestDb.Create("acme", _database))
        {
            acme.Remove(await acme.Set<Rol>().SingleAsync(r => r.Nombre == Rol.Proveedor));
            await acme.SaveChangesAsync();
        }

        for (var i = 0; i < 2; i++)
        {
            await using var db = AdminTestData.SinTenant(_database, out var tenant);
            var seeder = new PermisosSistemaSeeder(db, tenant, TimeProvider.System, NullLogger<PermisosSistemaSeeder>.Instance);
            await seeder.SeedAsync(CancellationToken.None);
        }

        await using var check = TestDb.Create("acme", _database);
        var proveedor = await check.Set<Rol>().Include(r => r.Permisos).SingleAsync(r => r.Nombre == Rol.Proveedor);
        Assert.True(proveedor.EsSistema);
        Assert.Equal("acme", proveedor.TenantId);
        Assert.Equal(5, proveedor.Permisos.Count);
    }
}
