using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Roles;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Tests.Features.Administracion;

public sealed class UpdateRolHandlerTests
{
    private readonly string _database = Guid.NewGuid().ToString();

    [Fact]
    public async Task A_company_admin_cannot_edit_system_roles()
    {
        await AdminTestData.ProvisionAsync(_database, ("demo", "admin@demo.co"));
        await using var db = TestDb.Create("demo", _database);
        var consulta = await db.Set<Rol>().Include(r => r.Permisos).SingleAsync(r => r.Nombre == Rol.Consulta);

        var result = await new UpdateRolHandler(db, new FakeCurrentUser(superAdmin: false))
            .HandleAsync(new UpdateRolCommand(consulta.Id, new UpdateRolRequest(consulta.Nombre, null, Matriz(consulta))), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Roles.Sistema", result.Error.Code);
    }

    [Fact]
    public async Task The_superadmin_edits_system_role_permissions_and_keeps_its_name()
    {
        await AdminTestData.ProvisionAsync(_database, ("demo", "admin@demo.co"));
        Guid id;
        await using (var db = TestDb.Create("demo", _database))
        {
            var consulta = await db.Set<Rol>().Include(r => r.Permisos).SingleAsync(r => r.Nombre == Rol.Consulta);
            id = consulta.Id;
            var matriz = Matriz(consulta)
                .Select(p => p.Modulo switch
                {
                    Modulos.Presupuestos => p with { Insertar = true, Editar = true },
                    Modulos.Apu => p with { Ver = false },
                    _ => p,
                })
                .ToList();

            var result = await new UpdateRolHandler(db, new FakeCurrentUser(superAdmin: true))
                .HandleAsync(new UpdateRolCommand(id, new UpdateRolRequest("Otro nombre", "Consulta y edición de presupuestos", matriz)), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(Rol.Consulta, result.Value.Nombre);
        }

        await using var verificacion = TestDb.Create("demo", _database);
        var guardado = await verificacion.Set<Rol>().Include(r => r.Permisos).SingleAsync(r => r.Id == id);
        var efectivos = guardado.PermisosEfectivos().ToHashSet();
        Assert.Contains(Modulos.Permiso(Modulos.Presupuestos, Accion.Editar), efectivos);
        Assert.DoesNotContain(Modulos.Permiso(Modulos.Apu, Accion.Ver), efectivos);

        // El arranque completa módulos nuevos, pero no devuelve uno que el superadministrador quitó.
        Assert.Equal(0, guardado.CompletarPermisos(Rol.PermisosSoloLectura()));
        Assert.Equal("Consulta y edición de presupuestos", guardado.Descripcion);
    }

    private static List<PermisoDto> Matriz(Rol rol) =>
        rol.Permisos.Select(p => new PermisoDto(p.Modulo, p.Ver, p.Insertar, p.Editar, p.Eliminar)).ToList();

    private sealed class FakeCurrentUser(bool superAdmin) : ICurrentUser
    {
        public Guid? Id => Guid.NewGuid();

        public bool IsSuperAdmin => superAdmin;

        public bool Can(string modulo, Accion accion) => true;
    }
}
