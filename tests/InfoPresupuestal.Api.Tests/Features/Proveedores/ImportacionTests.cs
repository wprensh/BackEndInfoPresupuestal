using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using InfoPresupuestal.Api.Features.Mantenimiento.Insumos;
using InfoPresupuestal.Api.Features.Mantenimiento.TiposInsumo;
using InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;
using InfoPresupuestal.Api.Features.Proveedores.Catalogo;
using InfoPresupuestal.Api.Features.Proveedores.Consulta;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Tests.Features.Proveedores;

public sealed class ImportacionTests
{
    private const string Cemento = "CEMENTO GRIS USO GENERAL 50 KG";
    private readonly string _database = Guid.NewGuid().ToString();

    [Fact]
    public async Task A_constructora_searches_every_supplier_and_filters_by_city()
    {
        await SembrarAsync();
        await using var acme = TestDb.Create("acme", _database);
        var handler = new BuscarProductosHandler(acme);

        var todos = (await handler.HandleAsync(new BuscarProductosQuery(new BuscarProductosRequest()), CancellationToken.None)).Value;
        var medellin = (await handler.HandleAsync(new BuscarProductosQuery(new BuscarProductosRequest(Ciudad: "medellín")), CancellationToken.None)).Value;

        Assert.Equal(2, todos.Total);
        Assert.All(todos.Items, p => Assert.Equal("Ferretería Sur S.A.S.", p.RazonSocial));
        var arena = Assert.Single(medellin.Items);
        Assert.Equal("ARENA DE RIO", arena.Descripcion);
        Assert.Null(Assert.Single(arena.Precios).Ciudad);
    }

    [Fact]
    public async Task The_review_proposes_and_the_import_applies_new_and_updated_prices_without_vat()
    {
        var (cemento, arena) = await SembrarAsync();
        await using var acme = TestDb.Create("acme", _database);

        var revision = (await new RevisarImportacionHandler(acme)
            .HandleAsync(new RevisarImportacionQuery(new RevisionImportacionRequest([cemento, arena, cemento])), CancellationToken.None)).Value;

        Assert.Collection(
            revision,
            c =>
            {
                Assert.Equal(ConsultaApi.Actualizar, c.Accion);
                Assert.Equal(28_000m, c.PrecioActual);
                Assert.Equal(30_000m, c.ValorSinIva);
                Assert.Equal("MAT-0001", c.InsumoCodigo);
            },
            a => Assert.Equal(ConsultaApi.Crear, a.Accion));

        var resultado = (await new ImportarPreciosHandler(acme, TimeProvider.System)
            .HandleAsync(new ImportarPreciosCommand(new ImportarRequest([new(cemento), new(arena)])), CancellationToken.None)).Value;

        Assert.Equal((1, 1, 0), (resultado.Creados, resultado.Actualizados, resultado.SinCambio));
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var insumos = await acme.Set<Insumo>().OrderBy(i => i.Codigo).ToListAsync();
        Assert.Equal(["MAT-0001", "MAT-0002"], insumos.Select(i => i.Codigo));
        var vigente = await acme.Set<InsumoPrecio>().Where(p => p.InsumoId == insumos[0].Id).OrderByDescending(p => p.FechaVigencia).FirstAsync();
        Assert.Equal((30_000m, hoy), (vigente.ValorUnitario, vigente.FechaVigencia));
        Assert.StartsWith("Ferretería Sur S.A.S. CEM-50 BOGOTÁ", vigente.Fuente);

        var otraVez = (await new RevisarImportacionHandler(acme)
            .HandleAsync(new RevisarImportacionQuery(new RevisionImportacionRequest([cemento, arena])), CancellationToken.None)).Value;
        Assert.All(otraVez, p => Assert.Equal(ConsultaApi.SinCambio, p.Accion));
    }

    /// <summary>Un proveedor con cemento (solo Bogotá, con IVA) y arena (nacional); acme ya tiene el cemento a $28.000.</summary>
    private async Task<(long Cemento, long Arena)> SembrarAsync()
    {
        var now = DateTimeOffset.UtcNow;
        await using (var global = TestDb.Create("ferreteria-sur", _database))
        {
            global.Add(TipoInsumo.Create(1, "MAT", "Material", "MAT"));
            global.Add(UnidadMedida.Create("UN", "Unidad"));
            global.Add(Empresa.Create("ferreteria-sur", "Ferretería Sur S.A.S.", null, null, null, now, TipoEmpresa.Proveedor));
            global.Add(Empresa.Create("acme", "Acme", null, null, null, now));
            await global.SaveChangesAsync();
        }

        long cemento, arena;
        await using (var sur = TestDb.Create("ferreteria-sur", _database))
        {
            var productos = new CreateProductoHandler(sur, TimeProvider.System);
            var precios = new CreatePrecioHandler(sur, TimeProvider.System);
            var c = (await productos.HandleAsync(new CreateProductoCommand(new("CEM-50", Cemento, 1, 1)), CancellationToken.None)).Value;
            var a = (await productos.HandleAsync(new CreateProductoCommand(new("ARE-1", "ARENA DE RIO", 1, 1)), CancellationToken.None)).Value;
            cemento = (await precios.HandleAsync(new CreatePrecioCommand(c.Id, new(35_700m, new DateOnly(2026, 10, 1), "Bogotá", IncluyeIva: true)), CancellationToken.None)).Value.Id;
            arena = (await precios.HandleAsync(new CreatePrecioCommand(a.Id, new(50_000m, new DateOnly(2026, 10, 1))), CancellationToken.None)).Value.Id;
        }

        await using (var acme = TestDb.Create("acme", _database))
        {
            var insumo = Insumo.Create("MAT-0001", 1, null, 1, Cemento, now);
            acme.Add(insumo);
            acme.Add(InsumoPrecio.Create(insumo.Id, 28_000m, null, new DateOnly(2026, 1, 1), null, now));
            await acme.SaveChangesAsync();
        }

        return (cemento, arena);
    }
}
