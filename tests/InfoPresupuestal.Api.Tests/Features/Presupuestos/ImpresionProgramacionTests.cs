using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using InfoPresupuestal.Api.Features.Presupuestos.Impresion;
using InfoPresupuestal.Api.Features.Presupuestos.Obras;
using InfoPresupuestal.Api.Features.Presupuestos.Programacion;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Tests.Features.Presupuestos;

public sealed class ImpresionProgramacionTests
{
    private static readonly DateOnly Inicio = new(2026, 11, 2);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private readonly string _database = Guid.NewGuid().ToString();

    [Fact]
    public void Print_and_schedule_modules_are_for_construction_companies()
    {
        var constructora = Modulos.ParaTipo(TipoEmpresa.Constructora).Select(m => m.Codigo).ToList();
        var proveedor = Modulos.ParaTipo(TipoEmpresa.Proveedor).Select(m => m.Codigo).ToList();

        Assert.Contains(Modulos.Impresion, constructora);
        Assert.Contains(Modulos.Programacion, constructora);
        Assert.DoesNotContain(Modulos.Impresion, proveedor);
        Assert.DoesNotContain(Modulos.Programacion, proveedor);
    }

    [Fact]
    public void The_logo_must_really_be_a_small_image()
    {
        var png = LogoImpresion.Parse($"data:image/png;base64,{Convert.ToBase64String(Png)}");
        var disfrazado = LogoImpresion.Parse($"data:image/png;base64,{Convert.ToBase64String("<svg>"u8.ToArray())}");
        var svg = LogoImpresion.Parse("data:image/svg+xml;base64,PHN2Zz4=");
        var grande = LogoImpresion.Parse($"data:image/png;base64,{Convert.ToBase64String([.. Png, .. new byte[LogoImpresion.MaxBytes]])}");

        Assert.True(png.IsSuccess);
        Assert.Equal("image/png", png.Value!.Tipo);
        Assert.False(disfrazado.IsSuccess);
        Assert.False(svg.IsSuccess);
        Assert.False(grande.IsSuccess);
        Assert.Null(LogoImpresion.Parse(null).Value);
    }

    [Fact]
    public async Task Printing_uses_the_company_data_until_the_letterhead_is_configured()
    {
        await using var db = await SembrarAsync("demo");
        var tenant = Tenant("demo");

        var inicial = await ImpresionApi.LeerAsync(db, tenant, CancellationToken.None);
        Assert.False(inicial.Personalizada);
        Assert.Equal("Constructora Demo S.A.S.", inicial.RazonSocial);
        Assert.Equal("900123456-7", inicial.Nit);

        var handler = new UpdateConfiguracionImpresionHandler(db, tenant, TimeProvider.System);
        var guardado = await handler.HandleAsync(
            new UpdateConfiguracionImpresionCommand(new ConfiguracionImpresionRequest(
                null, null, "Calle 1 # 2-3", "Bogotá", null, null, null, $"data:image/png;base64,{Convert.ToBase64String(Png)}", "#0D47A1",
                null, "Validez de la oferta: 30 días", "Ana Pérez", "Directora de obra", "TP 25202-12345")),
            CancellationToken.None);

        Assert.True(guardado.IsSuccess);
        Assert.True(guardado.Value.Personalizada);
        Assert.Equal("Constructora Demo S.A.S.", guardado.Value.RazonSocial);
        Assert.Equal("#0d47a1", guardado.Value.ColorPrimario);
        Assert.StartsWith("data:image/png;base64,", guardado.Value.Logo);

        // Otra empresa no ve el membrete de demo.
        await using var acme = TestDb.Create("acme", _database);
        Assert.False((await ImpresionApi.LeerAsync(acme, Tenant("acme"), CancellationToken.None)).Personalizada);
    }

    [Fact]
    public void Days_are_split_by_weight_and_add_up_to_the_total()
    {
        Assert.Equal([5, 3, 2], ProgramacionGenerador.Repartir([50m, 30m, 20m], 10));
        Assert.Equal([1, 1, 1], ProgramacionGenerador.Repartir([1000m, 1m, 1m], 3));
        Assert.Equal(30, ProgramacionGenerador.Repartir([7m, 0m, 13m, 2m], 30).Sum());
        Assert.Equal([2, 2], ProgramacionGenerador.Repartir([0m, 0m], 4));
    }

    [Fact]
    public async Task The_schedule_is_generated_from_chapters_and_items()
    {
        await using var db = await SembrarAsync("demo");
        var id = await CrearPresupuestoAsync(db);

        var result = await new GenerarProgramacionHandler(db, TimeProvider.System)
            .HandleAsync(new GenerarProgramacionCommand(id, new GenerarProgramacionRequest(Inicio, 20)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var p = result.Value;
        Assert.True(p.Existe);
        Assert.Equal(["project", "task", "task", "project", "task", "milestone"], p.Tareas.Select(t => t.Tipo));
        Assert.Equal(["1", "1.1", "1.2", "2", "2.1", null], p.Tareas.Select(t => t.Referencia));
        Assert.All(p.Tareas.Where(t => t.Tipo == "task"), t => Assert.Equal(p.Tareas.Single(c => c.Id == t.ParentId).Referencia, t.Referencia![..1]));
        Assert.Equal(Inicio.AddDays(20), p.Tareas[^1].FechaInicio);

        // Las actividades de cada capítulo cubren todo su plazo.
        foreach (var capitulo in p.Tareas.Where(t => t.Tipo == "project"))
        {
            var hijos = p.Tareas.Where(t => t.ParentId == capitulo.Id).ToList();
            Assert.Equal(capitulo.FechaInicio, hijos.Min(h => h.FechaInicio));
            Assert.Equal(capitulo.FechaFin, hijos.Max(h => h.FechaFin));
        }
        Assert.Equal(2, p.Vinculos.Count);

        // Una segunda generación no pisa el trabajo sin confirmación.
        var otra = await new GenerarProgramacionHandler(db, TimeProvider.System)
            .HandleAsync(new GenerarProgramacionCommand(id, new GenerarProgramacionRequest(Inicio, 20)), CancellationToken.None);
        Assert.Equal(ErrorType.Conflict, otra.Error!.Type);
    }

    [Fact]
    public async Task Saving_keeps_task_ids_and_drops_what_was_removed()
    {
        await using var db = await SembrarAsync("demo");
        var id = await CrearPresupuestoAsync(db);
        var handler = new GuardarProgramacionHandler(db, TimeProvider.System);
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var primero = await handler.HandleAsync(new GuardarProgramacionCommand(id, new ProgramacionRequest(
            Inicio, "semana", [Tarea(a, "PRELIMINARES", null, "project"), Tarea(b, "LOCALIZACIÓN", a), Tarea(c, "DESCAPOTE", a)],
            [new VinculoRequest(Guid.NewGuid(), b, c, 0, 1)])), CancellationToken.None);
        Assert.True(primero.IsSuccess);

        var segundo = await handler.HandleAsync(new GuardarProgramacionCommand(id, new ProgramacionRequest(
            Inicio, "dia", [Tarea(a, "PRELIMINARES", null, "project"), Tarea(b, "LOCALIZACIÓN Y REPLANTEO", a, progreso: 0.5m)], [])), CancellationToken.None);

        Assert.True(segundo.IsSuccess);
        Assert.Equal("dia", segundo.Value.Escala);
        Assert.Equal([a, b], segundo.Value.Tareas.Select(t => t.Id));
        Assert.Equal("LOCALIZACIÓN Y REPLANTEO", segundo.Value.Tareas[1].Texto);
        Assert.Equal(0.5m, segundo.Value.Tareas[1].Progreso);
        Assert.Empty(segundo.Value.Vinculos);
        Assert.Equal(2, await db.Set<ProgramacionTarea>().CountAsync());
    }

    [Fact]
    public async Task Invalid_structures_are_rejected()
    {
        await using var db = await SembrarAsync("demo");
        var id = await CrearPresupuestoAsync(db);
        var handler = new GuardarProgramacionHandler(db, TimeProvider.System);
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());

        var ciclo = await handler.HandleAsync(new GuardarProgramacionCommand(id, new ProgramacionRequest(
            Inicio, null, [Tarea(a, "A", b, "project"), Tarea(b, "B", a, "project")], [])), CancellationToken.None);
        var huerfano = await handler.HandleAsync(new GuardarProgramacionCommand(id, new ProgramacionRequest(
            Inicio, null, [Tarea(a, "A")], [new VinculoRequest(Guid.NewGuid(), a, b, 0)])), CancellationToken.None);
        var propio = await handler.HandleAsync(new GuardarProgramacionCommand(id, new ProgramacionRequest(
            Inicio, null, [Tarea(a, "A")], [new VinculoRequest(Guid.NewGuid(), a, a, 0)])), CancellationToken.None);

        Assert.Equal("Programacion.Ciclo", ciclo.Error!.Code);
        Assert.Equal("Programacion.VinculoInvalido", huerfano.Error!.Code);
        Assert.Equal("Programacion.VinculoInvalido", propio.Error!.Code);
    }

    [Fact]
    public async Task A_company_cannot_reach_another_company_schedule()
    {
        await using var db = await SembrarAsync("demo");
        var id = await CrearPresupuestoAsync(db);
        var tarea = Guid.NewGuid();
        await new GuardarProgramacionHandler(db, TimeProvider.System).HandleAsync(
            new GuardarProgramacionCommand(id, new ProgramacionRequest(Inicio, null, [Tarea(tarea, "A")], [])), CancellationToken.None);

        await using var acme = await SembrarAsync("acme");
        var ajeno = await new GetProgramacionHandler(acme).HandleAsync(new GetProgramacionQuery(id), CancellationToken.None);
        Assert.Equal(ErrorType.NotFound, ajeno.Error!.Type);

        // Reutilizar el id de una actividad de demo desde otro presupuesto es un conflicto, no una toma de control.
        var propio = await CrearPresupuestoAsync(acme);
        var robo = await new GuardarProgramacionHandler(acme, TimeProvider.System).HandleAsync(
            new GuardarProgramacionCommand(propio, new ProgramacionRequest(Inicio, null, [Tarea(tarea, "B")], [])), CancellationToken.None);
        Assert.Equal("Programacion.IdEnUso", robo.Error!.Code);
    }

    private static TareaRequest Tarea(Guid id, string texto, Guid? parent = null, string tipo = "task", decimal progreso = 0) =>
        new(id, parent, tipo, texto, null, Inicio, 3, progreso, null, null, null, 1000m, null, null);

    private static TenantContext Tenant(string id)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(id);
        return tenant;
    }

    private async Task<AppDbContext> SembrarAsync(string tenant)
    {
        var db = TestDb.Create(tenant, _database);
        if (!await db.Set<Empresa>().AnyAsync(e => e.TenantId == tenant))
        {
            db.Add(Empresa.Create(tenant, tenant == "demo" ? "Constructora Demo S.A.S." : "ACME", "900123456-7", null, null, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        return db;
    }

    private static async Task<Guid> CrearPresupuestoAsync(AppDbContext db)
    {
        var capitulos = new[]
        {
            PresupuestoCapitulo.Create(1, "Preliminares", [
                PresupuestoItem.Create(1, null, "Localización y replanteo", 1, 100, 1000),
                PresupuestoItem.Create(2, null, "Descapote", 1, 50, 2000)]),
            PresupuestoCapitulo.Create(2, "Cimentación", [PresupuestoItem.Create(1, null, "Concreto ciclópeo", 2, 10, 30000)]),
        };
        var presupuesto = Presupuesto.Create(
            $"PRE-{Random.Shared.Next(1, 9999):0000}",
            new EncabezadoPresupuesto("Casa de prueba", null, null, Inicio, null, PorcentajesAiu.PorDefecto, 20, null, null),
            capitulos,
            DateTimeOffset.UtcNow);
        db.Add(presupuesto);
        await db.SaveChangesAsync();
        return presupuesto.Id;
    }
}
