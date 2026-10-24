using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Roles;
using InfoPresupuestal.Api.Features.Mantenimiento.UnidadesMedida;
using InfoPresupuestal.Api.Features.Presupuestos;
using InfoPresupuestal.Api.Features.Presupuestos.Apus;
using InfoPresupuestal.Api.Features.Presupuestos.Obras;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Tests.Features.Presupuestos;

public sealed class PresupuestosTests
{
    private readonly string _database = Guid.NewGuid().ToString();

    [Fact]
    public void Apu_lines_use_the_excel_formulas()
    {
        // Valores de REPLANTEO en la hoja APU's.
        Assert.Equal(113505m, ApuCalculo.ValorParcial(ApuDetalle.Material(1, null, "MINERAL ROJO", 1, 23, 0.05m, 4700)));
        Assert.Equal(20240m, ApuCalculo.ValorParcial(ApuDetalle.Equipo(1, null, "RETROEXCAVADORA JD-510", 1, 0.23m, 88000)));
        Assert.Equal(286.784016m, ApuCalculo.ValorParcial(ApuDetalle.ManoObra(1, null, "OFICIAL", 42174.12m, 1.7m, 250)));
    }

    [Fact]
    public void Budget_totals_apply_aiu_and_vat_on_profit()
    {
        var t = PresupuestoCalculo.Totales(1_000_000m, new PorcentajesAiu(0.10m, 0.02m, 0.04m, 0.16m), anticipoPct: 0.12m);

        Assert.Equal(100_000m, t.Administracion);
        Assert.Equal(20_000m, t.Imprevistos);
        Assert.Equal(40_000m, t.Utilidad);
        Assert.Equal(160_000m, t.TotalAiu);
        Assert.Equal(6_400m, t.IvaUtilidad);
        Assert.Equal(1_166_400m, t.ValorTotal);
        Assert.Equal(139_968m, t.Anticipo);
    }

    [Fact]
    public async Task Items_reuse_the_apu_and_get_consecutive_codes()
    {
        var (apuId, unidad) = await SembrarApuAsync("demo");
        await using var db = TestDb.Create("demo", _database);

        var result = await new CreatePresupuestoHandler(db, TimeProvider.System).HandleAsync(
            new CreatePresupuestoCommand(Solicitud(
                new CapituloRequest("Preliminares", [new ItemRequest(apuId, null, null, 10, null), new ItemRequest(null, "ASEO", unidad, 2, 500)]),
                new CapituloRequest("Cimentación", [new ItemRequest(apuId, "REPLANTEO LOTE 2", null, 1, null)]))),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var p = result.Value;
        Assert.Equal("PRE-0001", p.Codigo);
        Assert.Equal(["1.1", "1.2", "2.1"], p.Capitulos.SelectMany(c => c.Items).Select(i => i.Codigo));
        var primero = p.Capitulos[0].Items[0];
        Assert.Equal("REPLANTEO", primero.Descripcion);
        Assert.Equal(1000m, primero.ValorUnitario);
        Assert.Equal(10_000m, primero.ValorTotal);
        Assert.Equal("REPLANTEO LOTE 2", p.Capitulos[1].Items[0].Descripcion);
        Assert.Equal(12_000m, p.Totales.CostoDirecto);
    }

    [Fact]
    public async Task A_manual_item_needs_description_unit_and_value()
    {
        await using var db = TestDb.Create("demo", _database);

        var result = await new CreatePresupuestoHandler(db, TimeProvider.System).HandleAsync(
            new CreatePresupuestoCommand(Solicitud(new CapituloRequest("Obra", [new ItemRequest(null, "SIN VALOR", null, 1, null)]))),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Presupuestos.ItemIncompleto", result.Error!.Code);
    }

    [Fact]
    public async Task An_apu_from_another_company_cannot_be_used()
    {
        var (apuAcme, _) = await SembrarApuAsync("acme");
        await using var db = TestDb.Create("demo", _database);

        var result = await new CreatePresupuestoHandler(db, TimeProvider.System).HandleAsync(
            new CreatePresupuestoCommand(Solicitud(new CapituloRequest("Obra", [new ItemRequest(apuAcme, null, null, 1, null)]))),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Presupuestos.ApuInvalido", result.Error!.Code);
    }

    [Fact]
    public async Task An_approved_budget_is_frozen_and_its_apu_cannot_be_deleted()
    {
        var (apuId, _) = await SembrarApuAsync("demo");
        await using var db = TestDb.Create("demo", _database);
        var creado = await new CreatePresupuestoHandler(db, TimeProvider.System).HandleAsync(
            new CreatePresupuestoCommand(Solicitud(new CapituloRequest("Obra", [new ItemRequest(apuId, null, null, 1, null)]))),
            CancellationToken.None);
        var id = creado.Value.Id;

        Assert.True((await new CambiarEstadoPresupuestoHandler(db, TimeProvider.System)
            .HandleAsync(new CambiarEstadoPresupuestoCommand(id, EstadoPresupuesto.Aprobado), CancellationToken.None)).IsSuccess);

        var editar = await new UpdatePresupuestoHandler(db, TimeProvider.System)
            .HandleAsync(new UpdatePresupuestoCommand(id, Solicitud()), CancellationToken.None);
        Assert.Equal(ErrorType.Conflict, editar.Error!.Type);

        var borrarApu = await new DeleteApuHandler(db).HandleAsync(new DeleteApuCommand(apuId), CancellationToken.None);
        Assert.Equal("Apus.EnUso", borrarApu.Error!.Code);
    }

    [Fact]
    public async Task Recalculating_brings_the_current_apu_cost()
    {
        var (apuId, unidad) = await SembrarApuAsync("demo");
        await using var db = TestDb.Create("demo", _database);
        var creado = await new CreatePresupuestoHandler(db, TimeProvider.System).HandleAsync(
            new CreatePresupuestoCommand(Solicitud(new CapituloRequest("Obra", [new ItemRequest(apuId, null, null, 3, null)]))),
            CancellationToken.None);

        var apu = await db.Set<Apu>().Include(a => a.Detalles).FirstAsync(a => a.Id == apuId);
        apu.Update(apu.Descripcion, unidad, apu.Fecha, null, true, [ApuDetalle.Equipo(1, null, "HERRAMIENTA", unidad, 1, 1500)], TimeProvider.System.GetUtcNow());
        await db.SaveChangesAsync();

        var antes = await new GetPresupuestoHandler(db).HandleAsync(new GetPresupuestoQuery(creado.Value.Id), CancellationToken.None);
        Assert.Equal(1, antes.Value.ItemsDesactualizados);

        var despues = await new RecalcularPresupuestoHandler(db, TimeProvider.System)
            .HandleAsync(new RecalcularPresupuestoCommand(creado.Value.Id), CancellationToken.None);
        Assert.Equal(0, despues.Value.ItemsDesactualizados);
        Assert.Equal(4_500m, despues.Value.Totales.CostoDirecto);
    }

    [Fact]
    public void System_roles_receive_new_modules_without_losing_their_changes()
    {
        var rol = Rol.Create(Rol.Consulta, null, [RolPermiso.Create(Modulos.Insumos, ver: true, insertar: true, editar: false, eliminar: false)], TimeProvider.System.GetUtcNow(), esSistema: true);

        var agregados = rol.CompletarPermisos(Rol.PermisosSoloLectura());

        Assert.Equal(Rol.PermisosSoloLectura().Count() - 1, agregados);
        Assert.True(rol.Permisos.Single(p => p.Modulo == Modulos.Insumos).Insertar);
        Assert.Contains(rol.Permisos, p => p.Modulo == Modulos.Presupuestos && p.Ver && !p.Editar);
        Assert.Equal(0, rol.CompletarPermisos(Rol.PermisosSoloLectura()));
    }

    private static PresupuestoRequest Solicitud(params CapituloRequest[] capitulos) =>
        new("Casa de prueba", "Cliente", null, null, null, null, null, null, null, null, null, null, capitulos);

    /// <summary>APU "REPLANTEO" de costo 1.000 (una línea de equipo) en la empresa indicada.</summary>
    private async Task<(Guid ApuId, short UnidadId)> SembrarApuAsync(string tenant)
    {
        await using var db = TestDb.Create(tenant, _database);
        var unidad = await db.Set<UnidadMedida>().FirstOrDefaultAsync();
        if (unidad is null)
        {
            unidad = UnidadMedida.Create("M2", "Metro cuadrado");
            db.Add(unidad);
            await db.SaveChangesAsync();
        }

        var apu = Apu.Create(
            $"{Apu.Prefijo}-0001", "REPLANTEO", unidad.Id, new DateOnly(2026, 10, 24), null,
            [ApuDetalle.Equipo(1, null, "HERRAMIENTA MENOR", unidad.Id, 1, 1000)], TimeProvider.System.GetUtcNow());
        db.Add(apu);
        await db.SaveChangesAsync();
        Assert.Equal(PresupuestosModulo.Equipo, apu.Detalles[0].TipoInsumoId);
        return (apu.Id, unidad.Id);
    }
}
