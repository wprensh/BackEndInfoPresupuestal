using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Presupuestos.Obras;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Programacion;

// ---------- Listar presupuestos con su programación ----------

public sealed record ListProgramacionesRequest(int Page = 1, int PageSize = 20, string? Search = null) : PageRequest(Page, PageSize, Search);

public sealed record ListProgramacionesQuery(ListProgramacionesRequest Filtro) : IQuery<PagedResponse<ProgramacionResumenResponse>>;

internal sealed class ListProgramacionesHandler(AppDbContext db) : IQueryHandler<ListProgramacionesQuery, PagedResponse<ProgramacionResumenResponse>>
{
    public async Task<Result<PagedResponse<ProgramacionResumenResponse>>> HandleAsync(ListProgramacionesQuery query, CancellationToken cancellationToken)
    {
        var f = query.Filtro;
        var presupuestos = db.Set<Presupuesto>().AsNoTracking().Where(p => p.Estado != EstadoPresupuesto.Anulado);
        if (f.SearchPattern is { } pattern)
        {
            presupuestos = presupuestos.Where(p =>
                EF.Functions.ILike(p.NombreObra, pattern) || EF.Functions.ILike(p.Codigo, pattern)
                || (p.Cliente != null && EF.Functions.ILike(p.Cliente, pattern)));
        }

        var pagina = await presupuestos.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Codigo)
            .Select(p => new { p.Id, p.Codigo, p.NombreObra, p.Cliente, p.Estado })
            .ToPagedAsync(f, cancellationToken);

        var ids = pagina.Items.Select(p => p.Id).ToList();
        var programaciones = await db.Set<ProgramacionObra>().AsNoTracking()
            .Where(p => ids.Contains(p.PresupuestoId))
            .Include(p => p.Tareas)
            .ToDictionaryAsync(p => p.PresupuestoId, cancellationToken);

        var items = pagina.Items.Select(p =>
        {
            if (!programaciones.TryGetValue(p.Id, out var prog))
            {
                return new ProgramacionResumenResponse(p.Id, p.Codigo, p.NombreObra, p.Cliente, p.Estado, false, null, null, 0, null, null);
            }

            var tareas = prog.Tareas.Where(t => t.Tipo != TipoTarea.Capitulo).ToList();
            return new ProgramacionResumenResponse(
                p.Id, p.Codigo, p.NombreObra, p.Cliente, p.Estado, true,
                prog.Tareas.Count == 0 ? prog.FechaInicio : prog.Tareas.Min(t => t.FechaInicio),
                prog.Tareas.Count == 0 ? null : prog.Tareas.Max(t => t.FechaFin),
                tareas.Count,
                ProgramacionCalculo.Avance(tareas),
                prog.ActualizadoEn);
        }).ToList();
        return new PagedResponse<ProgramacionResumenResponse>(items, pagina.Page, pagina.PageSize, pagina.Total);
    }
}

internal sealed class ListProgramacionesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ProgramacionApi.ListRoute, async ([AsParameters] ListProgramacionesRequest filtro, IQueryHandler<ListProgramacionesQuery, PagedResponse<ProgramacionResumenResponse>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListProgramacionesQuery(filtro), ct)).ToOk())
            .WithName("ListProgramaciones")
            .WithTags(ProgramacionApi.Tag)
            .RequirePermission(Modulos.Programacion, Accion.Ver);
}

internal static class ProgramacionCalculo
{
    /// <summary>Avance ponderado por valor (o por duración si no hay valores) de las actividades que no son resumen.</summary>
    public static decimal? Avance(IReadOnlyCollection<ProgramacionTarea> tareas)
    {
        if (tareas.Count == 0)
        {
            return null;
        }

        var porValor = tareas.Sum(t => t.Valor ?? 0) > 0;
        decimal Peso(ProgramacionTarea t) => porValor ? t.Valor ?? 0 : Math.Max(1, t.Duracion);
        var total = tareas.Sum(Peso);
        return total == 0 ? 0 : decimal.Round(tareas.Sum(t => Peso(t) * t.Progreso) / total, 4);
    }
}

// ---------- Consultar ----------

public sealed record GetProgramacionQuery(Guid PresupuestoId) : IQuery<ProgramacionResponse>;

internal sealed class GetProgramacionHandler(AppDbContext db) : IQueryHandler<GetProgramacionQuery, ProgramacionResponse>
{
    public async Task<Result<ProgramacionResponse>> HandleAsync(GetProgramacionQuery query, CancellationToken cancellationToken) =>
        await ProgramacionApi.LeerAsync(db, query.PresupuestoId, cancellationToken) is { } p ? p : PresupuestosApi.NotFound(query.PresupuestoId);
}

internal sealed class GetProgramacionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ProgramacionApi.Route("{id:guid}"), async (Guid id, IQueryHandler<GetProgramacionQuery, ProgramacionResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetProgramacionQuery(id), ct)).ToOk())
            .WithName("GetProgramacion")
            .WithTags(ProgramacionApi.Tag)
            .Produces<ProgramacionResponse>()
            .RequirePermission(Modulos.Programacion, Accion.Ver);
}

// ---------- Guardar el diagrama completo ----------

public sealed record GuardarProgramacionCommand(Guid PresupuestoId, ProgramacionRequest Datos) : ICommand<ProgramacionResponse>;

internal sealed class GuardarProgramacionHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<GuardarProgramacionCommand, ProgramacionResponse>
{
    public async Task<Result<ProgramacionResponse>> HandleAsync(GuardarProgramacionCommand command, CancellationToken cancellationToken)
    {
        var id = command.PresupuestoId;
        if (!await db.Set<Presupuesto>().AnyAsync(p => p.Id == id, cancellationToken))
        {
            return PresupuestosApi.NotFound(id);
        }

        var tareas = command.Datos.Tareas ?? [];
        var vinculos = command.Datos.Vinculos ?? [];
        if (ProgramacionApi.Validar(tareas, vinculos) is { } error)
        {
            return error;
        }

        // Los ids los genera el diagrama: no pueden ser de actividades de otro presupuesto (ni de otra empresa).
        var tareaIds = tareas.Select(t => t.Id).ToList();
        var vinculoIds = vinculos.Select(v => v.Id).ToList();
        if (await db.Set<ProgramacionTarea>().IgnoreQueryFilters().AnyAsync(t => tareaIds.Contains(t.Id) && t.PresupuestoId != id, cancellationToken)
            || await db.Set<ProgramacionVinculo>().IgnoreQueryFilters().AnyAsync(v => vinculoIds.Contains(v.Id) && v.PresupuestoId != id, cancellationToken))
        {
            return Error.Conflict("Programacion.IdEnUso", "Algún identificador de actividad o vínculo ya pertenece a otra programación.");
        }

        var now = timeProvider.GetUtcNow();
        var programacion = await db.Set<ProgramacionObra>()
            .Include(p => p.Tareas)
            .Include(p => p.Vinculos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.PresupuestoId == id, cancellationToken);
        if (programacion is null)
        {
            programacion = ProgramacionObra.Create(id, now);
            db.Add(programacion);
        }

        programacion.Reemplazar(
            command.Datos.FechaInicio,
            command.Datos.Escala ?? programacion.Escala,
            tareas.Select(ProgramacionApi.ToDatos).ToList(),
            vinculos.Select(v => new VinculoDatos(v.Id, v.Origen, v.Destino, v.Tipo, v.Desfase)).ToList(),
            now);
        await db.SaveChangesAsync(cancellationToken);
        return (await ProgramacionApi.LeerAsync(db, id, cancellationToken))!;
    }
}

internal sealed class GuardarProgramacionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut(ProgramacionApi.Route("{id:guid}"), async (Guid id, ProgramacionRequest datos, ICommandHandler<GuardarProgramacionCommand, ProgramacionResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GuardarProgramacionCommand(id, datos), ct)).ToOk())
            .WithName("GuardarProgramacion")
            .WithTags(ProgramacionApi.Tag)
            .WithRequestValidation<ProgramacionRequest>()
            .Produces<ProgramacionResponse>()
            .RequirePermission(Modulos.Programacion, Accion.Editar);
}

// ---------- Generar desde el presupuesto ----------

public sealed record GenerarProgramacionCommand(Guid PresupuestoId, GenerarProgramacionRequest Datos) : ICommand<ProgramacionResponse>;

internal sealed class GenerarProgramacionHandler(AppDbContext db, TimeProvider timeProvider) : ICommandHandler<GenerarProgramacionCommand, ProgramacionResponse>
{
    public async Task<Result<ProgramacionResponse>> HandleAsync(GenerarProgramacionCommand command, CancellationToken cancellationToken)
    {
        var id = command.PresupuestoId;
        var presupuesto = await db.Set<Presupuesto>().AsNoTracking()
            .Include(p => p.Capitulos).ThenInclude(c => c.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (presupuesto is null)
        {
            return PresupuestosApi.NotFound(id);
        }

        if (presupuesto.Capitulos.Count == 0)
        {
            return Error.Validation("Programacion.SinCapitulos", "El presupuesto no tiene capítulos para programar.");
        }

        var programacion = await db.Set<ProgramacionObra>()
            .Include(p => p.Tareas)
            .Include(p => p.Vinculos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.PresupuestoId == id, cancellationToken);
        if (programacion is not null && programacion.Tareas.Count > 0 && !command.Datos.Reemplazar)
        {
            return ProgramacionApi.YaExiste(id);
        }

        var now = timeProvider.GetUtcNow();
        var inicio = command.Datos.FechaInicio ?? presupuesto.Fecha;
        var duracion = command.Datos.DuracionDias ?? presupuesto.DuracionDias ?? ProgramacionGenerador.DuracionPorDefecto;
        var (tareas, vinculos) = ProgramacionGenerador.Generar(presupuesto.Capitulos, inicio, duracion, () => Guid.CreateVersion7(now));

        if (programacion is null)
        {
            programacion = ProgramacionObra.Create(id, now);
            db.Add(programacion);
        }

        var escala = duracion <= 21 ? EscalaProgramacion.Dia : duracion <= 240 ? EscalaProgramacion.Semana : EscalaProgramacion.Mes;
        programacion.Reemplazar(inicio, escala, tareas, vinculos, now);
        await db.SaveChangesAsync(cancellationToken);
        return (await ProgramacionApi.LeerAsync(db, id, cancellationToken))!;
    }
}

internal sealed class GenerarProgramacionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost($"{ProgramacionApi.Route("{id:guid}")}/generar", async (Guid id, GenerarProgramacionRequest datos, ICommandHandler<GenerarProgramacionCommand, ProgramacionResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GenerarProgramacionCommand(id, datos), ct)).ToOk())
            .WithName("GenerarProgramacion")
            .WithTags(ProgramacionApi.Tag)
            .WithRequestValidation<GenerarProgramacionRequest>()
            .Produces<ProgramacionResponse>()
            .RequirePermission(Modulos.Programacion, Accion.Insertar);
}

// ---------- Eliminar ----------

public sealed record DeleteProgramacionCommand(Guid PresupuestoId) : ICommand<Done>;

internal sealed class DeleteProgramacionHandler(AppDbContext db) : ICommandHandler<DeleteProgramacionCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(DeleteProgramacionCommand command, CancellationToken cancellationToken)
    {
        var programacion = await db.Set<ProgramacionObra>()
            .Include(p => p.Tareas)
            .Include(p => p.Vinculos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.PresupuestoId == command.PresupuestoId, cancellationToken);
        if (programacion is null)
        {
            return Error.NotFound("Programacion.NotFound", "El presupuesto no tiene programación.");
        }

        db.Remove(programacion);
        await db.SaveChangesAsync(cancellationToken);
        return new Done();
    }
}

internal sealed class DeleteProgramacionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete(ProgramacionApi.Route("{id:guid}"), async (Guid id, ICommandHandler<DeleteProgramacionCommand, Done> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DeleteProgramacionCommand(id), ct)).ToNoContent())
            .WithName("DeleteProgramacion")
            .WithTags(ProgramacionApi.Tag)
            .RequirePermission(Modulos.Programacion, Accion.Eliminar);
}
