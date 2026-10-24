using System.Security.Claims;
using InfoPresupuestal.Api.Common.Security;

namespace InfoPresupuestal.Api.Tests.Common.Security;

public sealed class PermissionEvaluatorTests
{
    [Fact]
    public void Allows_only_the_actions_in_the_token()
    {
        var user = User(Modulos.Permiso(Modulos.Insumos, Accion.Ver), Modulos.Permiso(Modulos.Insumos, Accion.Editar));

        Assert.True(PermissionEvaluator.Allows(user, Modulos.Insumos, Accion.Ver));
        Assert.True(PermissionEvaluator.Allows(user, Modulos.Insumos, Accion.Editar));
        Assert.False(PermissionEvaluator.Allows(user, Modulos.Insumos, Accion.Insertar));
        Assert.False(PermissionEvaluator.Allows(user, Modulos.Insumos, Accion.Eliminar));
        Assert.False(PermissionEvaluator.Allows(user, Modulos.Usuarios, Accion.Ver));
    }

    [Fact]
    public void A_company_role_never_writes_global_catalogs_or_platform_modules()
    {
        // Aunque el token trajera el permiso, los catálogos globales y las empresas son del superadministrador.
        var user = User(Modulos.Permiso(Modulos.UnidadesMedida, Accion.Insertar), Modulos.Permiso(Modulos.Empresas, Accion.Ver));

        Assert.False(PermissionEvaluator.Allows(user, Modulos.UnidadesMedida, Accion.Insertar));
        Assert.False(PermissionEvaluator.Allows(user, Modulos.Empresas, Accion.Ver));
    }

    [Fact]
    public void The_superadmin_can_do_everything()
    {
        var user = User();
        ((ClaimsIdentity)user.Identity!).AddClaim(new Claim(AppClaims.SuperAdmin, "true"));

        Assert.True(PermissionEvaluator.Allows(user, Modulos.UnidadesMedida, Accion.Eliminar));
        Assert.True(PermissionEvaluator.Allows(user, Modulos.Empresas, Accion.Insertar));
    }

    [Fact]
    public void An_anonymous_user_has_no_permissions()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AppClaims.Permission, "insumos:ver")]));

        Assert.False(PermissionEvaluator.Allows(anonymous, Modulos.Insumos, Accion.Ver));
    }

    private static ClaimsPrincipal User(params string[] permisos) =>
        new(new ClaimsIdentity(permisos.Select(p => new Claim(AppClaims.Permission, p)), "test"));
}
