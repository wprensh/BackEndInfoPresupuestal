namespace InfoPresupuestal.Api.Common.Endpoints;

/// <summary>Cada slice expone su ruta HTTP implementando esta interfaz.</summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
