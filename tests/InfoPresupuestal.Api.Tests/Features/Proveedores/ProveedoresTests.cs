using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using InfoPresupuestal.Api.Features.Administracion.Roles;
using InfoPresupuestal.Api.Features.Mantenimiento.TiposInsumo;
using InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;
using InfoPresupuestal.Api.Features.Proveedores.Catalogo;
using InfoPresupuestal.Api.Features.Proveedores.Directorio;
using InfoPresupuestal.Api.Tests.Features.Administracion;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Tests.Features.Proveedores;

public sealed class ProveedoresTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 25);
    private readonly string _database = Guid.NewGuid().ToString();

    [Fact]
    public void Each_company_type_only_gets_its_own_modules()
    {
        var constructora = Modulos.ParaTipo(TipoEmpresa.Constructora).Select(m => m.Codigo).ToList();
        var proveedor = Modulos.ParaTipo(TipoEmpresa.Proveedor).Select(m => m.Codigo).ToList();

        Assert.Contains(Modulos.Proveedores, constructora);
        Assert.DoesNotContain(Modulos.CatalogoProveedor, constructora);
        Assert.Equal(["usuarios", "roles", "unidades-medida", "tipos-insumo", "catalogo-proveedor"], proveedor);
    }

    [Fact]
    public async Task A_constructora_admin_creates_a_supplier_account_with_its_own_roles()
    {
        await AdminTestData.ProvisionAsync(_database, ("acme", "admin@acme.co"));
        var tenant = new TenantContext();
        tenant.SetTenant("acme");
        await using var db = TestDb.Create(tenant, _database);
        var handler = new CreateProveedorHandler(db, new EmpresaProvisioner(db, tenant, AdminTestData.Hasher, TimeProvider.System), TimeProvider.System);

        var result = await handler.HandleAsync(
            new CreateProveedorCommand("ferreteria-sur", "Ferretería Sur S.A.S.", null, null, null, "Ventas", "ventas@sur.co", AdminTestData.Password),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("acme", tenant.TenantId);
        var empresa = await db.Set<Empresa>().SingleAsync(e => e.TenantId == "ferreteria-sur");
        Assert.Equal(TipoEmpresa.Proveedor, empresa.Tipo);

        await using var sur = TestDb.Create("ferreteria-sur", _database);
        var roles = await sur.Set<Rol>().Include(r => r.Permisos).OrderBy(r => r.Nombre).ToListAsync();
        Assert.Equal([Rol.Administrador, Rol.Consulta], roles.Select(r => r.Nombre));
        Assert.Contains(roles[0].Permisos, p => p is { Modulo: "catalogo-proveedor", Eliminar: true });
        Assert.DoesNotContain(roles[0].Permisos, p => p.Modulo is "insumos" or "apu" or "presupuestos");
    }

    [Fact]
    public async Task Product_codes_are_unique_per_supplier()
    {
        await SembrarCatalogosAsync();
        await using var db = TestDb.Create("ferreteria-sur", _database);
        var handler = new CreateProductoHandler(db, TimeProvider.System);

        var creado = await handler.HandleAsync(new CreateProductoCommand(Producto(" cem-50 ")), CancellationToken.None);
        var duplicado = await handler.HandleAsync(new CreateProductoCommand(Producto("CEM-50")), CancellationToken.None);

        Assert.Equal("CEM-50", creado.Value.Codigo);
        Assert.False(duplicado.IsSuccess);
        Assert.Equal(ErrorType.Conflict, duplicado.Error.Type);

        await using var otro = TestDb.Create("ferreteria-norte", _database);
        Assert.True((await new CreateProductoHandler(otro, TimeProvider.System).HandleAsync(new CreateProductoCommand(Producto("CEM-50")), CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task The_list_shows_the_latest_price_per_city_with_and_without_vat()
    {
        await SembrarCatalogosAsync();
        await using var db = TestDb.Create("ferreteria-sur", _database);
        var producto = (await new CreateProductoHandler(db, TimeProvider.System).HandleAsync(new CreateProductoCommand(Producto("CEM-50")), CancellationToken.None)).Value;
        var precios = new CreatePrecioHandler(db, TimeProvider.System);

        await precios.HandleAsync(new CreatePrecioCommand(producto.Id, new PrecioProveedorRequest(30_000m, Hoy.AddDays(-30))), CancellationToken.None);
        await precios.HandleAsync(new CreatePrecioCommand(producto.Id, new PrecioProveedorRequest(32_000m, Hoy)), CancellationToken.None);
        await precios.HandleAsync(new CreatePrecioCommand(producto.Id, new PrecioProveedorRequest(35_700m, Hoy, " bogotá d.c. ", IncluyeIva: true)), CancellationToken.None);
        var repetido = await precios.HandleAsync(new CreatePrecioCommand(producto.Id, new PrecioProveedorRequest(1m, Hoy, "BOGOTÁ D.C.")), CancellationToken.None);

        var lista = (await new ListProductosHandler(db).HandleAsync(new ListProductosQuery(new ListProductosRequest()), CancellationToken.None)).Value;

        Assert.False(repetido.IsSuccess);
        Assert.Equal(ErrorType.Conflict, repetido.Error.Type);
        var vigentes = Assert.Single(lista.Items).Precios;
        Assert.Collection(
            vigentes,
            nacional =>
            {
                Assert.Null(nacional.Ciudad);
                Assert.Equal(32_000m, nacional.ValorSinIva);
                Assert.Equal(38_080m, nacional.ValorConIva);
            },
            bogota =>
            {
                Assert.Equal("BOGOTÁ D.C.", bogota.Ciudad);
                Assert.Equal(30_000m, bogota.ValorSinIva);
                Assert.Equal(35_700m, bogota.ValorConIva);
            });
    }

    private static ProductoProveedorRequest Producto(string codigo) => new(codigo, "CEMENTO GRIS USO GENERAL 50 KG", 1, 1, "ARGOS", "CEMENTOS");

    private async Task SembrarCatalogosAsync()
    {
        await using var db = TestDb.Create("ferreteria-sur", _database);
        db.Add(TipoInsumo.Create(1, "MAT", "Material", "MAT"));
        db.Add(UnidadMedida.Create("UN", "Unidad"));
        await db.SaveChangesAsync();
    }
}
