using System.Linq.Expressions;

namespace InfoPresupuestal.Api.Features.Administracion.Empresas;

public sealed record EmpresaResponse(
    Guid Id,
    string TenantId,
    string RazonSocial,
    string? Nit,
    string? Email,
    string? Telefono,
    bool Activo,
    DateTimeOffset CreadoEn);

internal static class EmpresaMapping
{
    public static readonly Expression<Func<Empresa, EmpresaResponse>> ToResponse = e =>
        new EmpresaResponse(e.Id, e.TenantId, e.RazonSocial, e.Nit, e.Email, e.Telefono, e.Activo, e.CreadoEn);

    private static readonly Func<Empresa, EmpresaResponse> Compiled = ToResponse.Compile();

    public static EmpresaResponse AsResponse(this Empresa empresa) => Compiled(empresa);
}

internal static class EmpresasApi
{
    public const string Route = $"{AdministracionApi.Route}/empresas";

    public static Common.Abstractions.Error NotFound(Guid id) =>
        Common.Abstractions.Error.NotFound("Empresas.NotFound", $"No existe una empresa con id '{id}'.");
}
