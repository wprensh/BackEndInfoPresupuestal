namespace InfoPresupuestal.Api.Common.Endpoints;

/// <summary>
/// Cada slice expone su ruta HTTP implementando esta interfaz. Por defecto queda dentro del grupo
/// que exige usuario autenticado y tenant resuelto.
/// </summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}

/// <summary>Endpoint sin autenticación ni tenant (por ejemplo el login).</summary>
public interface IPublicEndpoint : IEndpoint;

/// <summary>Endpoint autenticado que no depende de un tenant (administración de la plataforma).</summary>
public interface IPlatformEndpoint : IEndpoint;
