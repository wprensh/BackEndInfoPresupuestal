using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Roles;

namespace InfoPresupuestal.Api.Tests.Features.Administracion;

public sealed class RolValidatorTests
{
    private readonly CreateRolValidator _validator = new();

    [Fact]
    public void Accepts_company_modules_with_any_action()
    {
        var result = _validator.Validate(Rol(new PermisoDto(Modulos.Insumos, true, true, true, true), new PermisoDto(Modulos.UnidadesMedida, true, false, false, false)));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(Modulos.UnidadesMedida)]
    [InlineData(Modulos.TiposInsumo)]
    public void Global_catalogs_only_allow_ver(string modulo)
    {
        var result = _validator.Validate(Rol(new PermisoDto(modulo, true, true, false, false)));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(Modulos.Empresas)]
    [InlineData("no-existe")]
    public void Rejects_platform_or_unknown_modules(string modulo)
    {
        var result = _validator.Validate(Rol(new PermisoDto(modulo, true, false, false, false)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_a_module_listed_twice()
    {
        var result = _validator.Validate(Rol(new PermisoDto(Modulos.Insumos, true, false, false, false), new PermisoDto(Modulos.Insumos, true, true, false, false)));

        Assert.False(result.IsValid);
    }

    private static CreateRolCommand Rol(params PermisoDto[] permisos) => new("Compras", null, permisos);
}
