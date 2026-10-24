using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Roles;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Tests.Features.Administracion;

public sealed class UpdateUsuarioHandlerTests
{
    private readonly string _database = Guid.NewGuid().ToString();

    [Fact]
    public async Task A_user_cannot_deactivate_themselves()
    {
        await AdminTestData.ProvisionAsync(_database, ("demo", "admin@demo.co"));
        await using var db = TestDb.Create("demo", _database);
        var admin = await db.Set<Usuario>().SingleAsync();

        var result = await new UpdateUsuarioHandler(db, new FakeCurrentUser(admin.Id))
            .HandleAsync(new UpdateUsuarioCommand(admin.Id, new UpdateUsuarioRequest("Admin", admin.RolId, Activo: false)), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task Cannot_assign_a_role_from_another_company()
    {
        await AdminTestData.ProvisionAsync(_database, ("acme", "admin@acme.co"), ("demo", "admin@demo.co"));
        Guid rolAcme;
        await using (var acme = TestDb.Create("acme", _database))
        {
            rolAcme = (await acme.Set<Rol>().FirstAsync(r => r.Nombre == Rol.Consulta)).Id;
        }

        await using var demo = TestDb.Create("demo", _database);
        var result = await new CreateUsuarioHandler(demo, AdminTestData.Hasher, TimeProvider.System)
            .HandleAsync(new CreateUsuarioCommand("nuevo@demo.co", "Nuevo", rolAcme, AdminTestData.Password), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Usuarios.RolInvalido", result.Error.Code);
    }

    private sealed class FakeCurrentUser(Guid id) : ICurrentUser
    {
        public Guid? Id => id;

        public bool IsSuperAdmin => false;

        public bool Can(string modulo, Accion accion) => true;
    }
}
