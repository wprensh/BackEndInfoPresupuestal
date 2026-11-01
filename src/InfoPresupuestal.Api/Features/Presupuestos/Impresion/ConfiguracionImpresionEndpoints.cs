using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Presupuestos.Obras;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Presupuestos.Impresion;

// ---------- Consultar el membrete ----------

public sealed record GetConfiguracionImpresionQuery : IQuery<ConfiguracionImpresionResponse>;

internal sealed class GetConfiguracionImpresionHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<GetConfiguracionImpresionQuery, ConfiguracionImpresionResponse>
{
    public async Task<Result<ConfiguracionImpresionResponse>> HandleAsync(GetConfiguracionImpresionQuery query, CancellationToken cancellationToken) =>
        await ImpresionApi.LeerAsync(db, tenant, cancellationToken);
}

internal sealed class GetConfiguracionImpresionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ImpresionApi.Route, async (IQueryHandler<GetConfiguracionImpresionQuery, ConfiguracionImpresionResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetConfiguracionImpresionQuery(), ct)).ToOk())
            .WithName("GetConfiguracionImpresion")
            .WithTags(ImpresionApi.Tag)
            .Produces<ConfiguracionImpresionResponse>()
            .RequirePermission(Modulos.Impresion, Accion.Ver);
}

// ---------- Guardar el membrete ----------

/// <param name="Logo">Data URL de la imagen (PNG, JPG o WebP, máx. 512 KB); null o vacío quita el logo.</param>
public sealed record ConfiguracionImpresionRequest(
    string? RazonSocial,
    string? Nit,
    string? Direccion,
    string? Ciudad,
    string? Telefono,
    string? Email,
    string? SitioWeb,
    string? Logo,
    string? ColorPrimario,
    string? Encabezado,
    string? PiePagina,
    string? FirmaNombre,
    string? FirmaCargo,
    string? FirmaMatricula,
    bool MostrarApu = true,
    bool MostrarParticipacion = true,
    bool MostrarValorLetras = true);

internal sealed class ConfiguracionImpresionRequestValidator : AbstractValidator<ConfiguracionImpresionRequest>
{
    public ConfiguracionImpresionRequestValidator()
    {
        RuleFor(r => r.RazonSocial).MaximumLength(ConfiguracionImpresionConfiguration.TextoMaxLength);
        RuleFor(r => r.Nit).MaximumLength(20);
        RuleFor(r => r.Direccion).MaximumLength(ConfiguracionImpresionConfiguration.TextoMaxLength);
        RuleFor(r => r.Ciudad).MaximumLength(80);
        RuleFor(r => r.Telefono).MaximumLength(30);
        RuleFor(r => r.Email).EmailAddress().MaximumLength(ConfiguracionImpresionConfiguration.TextoMaxLength).When(r => !string.IsNullOrWhiteSpace(r.Email));
        RuleFor(r => r.SitioWeb).MaximumLength(ConfiguracionImpresionConfiguration.TextoMaxLength);
        RuleFor(r => r.ColorPrimario).Matches("^#[0-9a-fA-F]{6}$").When(r => !string.IsNullOrWhiteSpace(r.ColorPrimario))
            .WithMessage("El color debe tener el formato #RRGGBB.");
        RuleFor(r => r.Encabezado).MaximumLength(ConfiguracionImpresionConfiguration.NotaMaxLength);
        RuleFor(r => r.PiePagina).MaximumLength(ConfiguracionImpresionConfiguration.NotaMaxLength);
        RuleFor(r => r.FirmaNombre).MaximumLength(120);
        RuleFor(r => r.FirmaCargo).MaximumLength(120);
        RuleFor(r => r.FirmaMatricula).MaximumLength(60);
    }
}

public sealed record UpdateConfiguracionImpresionCommand(ConfiguracionImpresionRequest Datos) : ICommand<ConfiguracionImpresionResponse>;

internal sealed class UpdateConfiguracionImpresionHandler(AppDbContext db, ITenantContext tenant, TimeProvider timeProvider)
    : ICommandHandler<UpdateConfiguracionImpresionCommand, ConfiguracionImpresionResponse>
{
    public async Task<Result<ConfiguracionImpresionResponse>> HandleAsync(UpdateConfiguracionImpresionCommand command, CancellationToken cancellationToken)
    {
        var r = command.Datos;
        var logo = LogoImpresion.Parse(r.Logo);
        if (!logo.IsSuccess)
        {
            return logo.Error;
        }

        var datos = new DatosImpresion(
            r.RazonSocial, r.Nit, r.Direccion, r.Ciudad, r.Telefono, r.Email, r.SitioWeb,
            string.IsNullOrWhiteSpace(r.ColorPrimario) ? ConfiguracionImpresion.ColorPorDefecto : r.ColorPrimario,
            r.Encabezado, r.PiePagina, r.FirmaNombre, r.FirmaCargo, r.FirmaMatricula,
            r.MostrarApu, r.MostrarParticipacion, r.MostrarValorLetras);

        var now = timeProvider.GetUtcNow();
        var config = await db.Set<ConfiguracionImpresion>().FirstOrDefaultAsync(cancellationToken);
        if (config is null)
        {
            db.Add(ConfiguracionImpresion.Create(datos, logo.Value, now));
        }
        else
        {
            config.Update(datos, logo.Value, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ImpresionApi.LeerAsync(db, tenant, cancellationToken);
    }
}

internal sealed class UpdateConfiguracionImpresionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut(ImpresionApi.Route, async (ConfiguracionImpresionRequest datos, ICommandHandler<UpdateConfiguracionImpresionCommand, ConfiguracionImpresionResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdateConfiguracionImpresionCommand(datos), ct)).ToOk())
            .WithName("UpdateConfiguracionImpresion")
            .WithTags(ImpresionApi.Tag)
            .WithRequestValidation<ConfiguracionImpresionRequest>()
            .Produces<ConfiguracionImpresionResponse>()
            .RequirePermission(Modulos.Impresion, Accion.Editar);
}

// ---------- Presupuesto listo para imprimir ----------

/// <summary>Todo lo que necesita la hoja impresa: membrete de la empresa y presupuesto con capítulos e ítems.</summary>
public sealed record PresupuestoImpresionResponse(ConfiguracionImpresionResponse Empresa, PresupuestoResponse Presupuesto);

public sealed record GetPresupuestoImpresionQuery(Guid Id) : IQuery<PresupuestoImpresionResponse>;

internal sealed class GetPresupuestoImpresionHandler(AppDbContext db, ITenantContext tenant)
    : IQueryHandler<GetPresupuestoImpresionQuery, PresupuestoImpresionResponse>
{
    public async Task<Result<PresupuestoImpresionResponse>> HandleAsync(GetPresupuestoImpresionQuery query, CancellationToken cancellationToken)
    {
        var presupuesto = await PresupuestosApi.LeerAsync(db, query.Id, cancellationToken);
        if (presupuesto is null)
        {
            return PresupuestosApi.NotFound(query.Id);
        }

        return new PresupuestoImpresionResponse(await ImpresionApi.LeerAsync(db, tenant, cancellationToken), presupuesto);
    }
}

internal sealed class GetPresupuestoImpresionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet($"{PresupuestosApi.Route}/{{id:guid}}/impresion", async (Guid id, IQueryHandler<GetPresupuestoImpresionQuery, PresupuestoImpresionResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetPresupuestoImpresionQuery(id), ct)).ToOk())
            .WithName("GetPresupuestoImpresion")
            .WithTags(ImpresionApi.Tag)
            .Produces<PresupuestoImpresionResponse>()
            .RequirePermission(Modulos.Presupuestos, Accion.Ver);
}
