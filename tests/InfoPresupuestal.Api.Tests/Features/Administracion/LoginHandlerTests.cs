using System.IdentityModel.Tokens.Jwt;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Autenticacion;
using InfoPresupuestal.Api.Features.Administracion.Roles;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InfoPresupuestal.Api.Tests.Features.Administracion;

public sealed class LoginHandlerTests
{
    private readonly string _database = Guid.NewGuid().ToString();

    private static readonly JwtTokenService Tokens = new(
        Options.Create(new JwtOptions { SigningKey = "clave-de-pruebas-de-al-menos-32-caracteres" }),
        TimeProvider.System);

    [Fact]
    public async Task Logs_in_to_the_requested_company_only()
    {
        // El mismo correo existe en dos empresas: la empresa elegida decide a qué usuario entra.
        await AdminTestData.ProvisionAsync(_database, ("acme", "admin@correo.co"), ("demo", "admin@correo.co"));

        var result = await LoginAsync(new LoginCommand("demo", "ADMIN@correo.co", AdminTestData.Password));

        Assert.True(result.IsSuccess);
        Assert.Equal("demo", result.Value.Usuario.Empresa);
        Assert.Equal(Rol.Administrador, result.Value.Usuario.Rol);
        Assert.Contains(Modulos.Permiso(Modulos.Usuarios, Accion.Eliminar), result.Value.Permisos);
        Assert.DoesNotContain(Modulos.Permiso(Modulos.Empresas, Accion.Ver), result.Value.Permisos);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.Token);
        Assert.Equal("demo", jwt.Claims.Single(c => c.Type == AppClaims.Tenant).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == AppClaims.SuperAdmin);
    }

    [Theory]
    [InlineData("demo", "admin@correo.co", "Incorrecta1")]
    [InlineData("otra", "admin@correo.co", AdminTestData.Password)]
    [InlineData("demo", "nadie@correo.co", AdminTestData.Password)]
    public async Task Rejects_bad_credentials_with_the_same_error(string empresa, string email, string password)
    {
        await AdminTestData.ProvisionAsync(_database, ("demo", "admin@correo.co"));

        var result = await LoginAsync(new LoginCommand(empresa, email, password));

        Assert.False(result.IsSuccess);
        Assert.Equal(AutenticacionApi.CredencialesInvalidas, result.Error);
    }

    [Fact]
    public async Task Rejects_an_inactive_user()
    {
        await AdminTestData.ProvisionAsync(_database, ("demo", "admin@correo.co"));
        await using (var db = TestDb.Create("demo", _database))
        {
            var usuario = await db.Set<Usuario>().SingleAsync();
            usuario.Update(usuario.Nombre, usuario.RolId, activo: false);
            await db.SaveChangesAsync();
        }

        var result = await LoginAsync(new LoginCommand("demo", "admin@correo.co", AdminTestData.Password));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
    }

    [Fact]
    public async Task Records_the_last_access()
    {
        await AdminTestData.ProvisionAsync(_database, ("demo", "admin@correo.co"));

        await LoginAsync(new LoginCommand("demo", "admin@correo.co", AdminTestData.Password));

        await using var db = TestDb.Create("demo", _database);
        Assert.NotNull((await db.Set<Usuario>().SingleAsync()).UltimoAcceso);
    }

    private async Task<Result<LoginResponse>> LoginAsync(LoginCommand command)
    {
        await using var db = AdminTestData.SinTenant(_database, out var tenant);
        return await new LoginHandler(db, tenant, AdminTestData.Hasher, Tokens, TimeProvider.System).HandleAsync(command, CancellationToken.None);
    }
}
