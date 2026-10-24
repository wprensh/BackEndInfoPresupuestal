using FluentValidation;
using InfoPresupuestal.Api.Common.Abstractions;
using InfoPresupuestal.Api.Common.Endpoints;
using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Persistence;
using InfoPresupuestal.Api.Common.Security;
using InfoPresupuestal.Api.Common.Validation;
using InfoPresupuestal.Api.Features.Administracion.Empresas;
using InfoPresupuestal.Api.Features.Administracion.Usuarios;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InfoPresupuestal.Api.Features.Administracion.Autenticacion;

/// <summary>Inicio de sesión: <paramref name="Empresa"/> es el identificador del tenant (por ejemplo <c>demo</c>).</summary>
public sealed record LoginCommand(string Empresa, string Email, string Password) : ICommand<LoginResponse>;

public sealed record LoginResponse(string Token, DateTimeOffset ExpiraEn, SesionUsuario Usuario, IReadOnlyCollection<string> Permisos);

internal sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(c => c.Empresa).NotEmpty().MaximumLength(AppDbContext.TenantIdMaxLength);
        RuleFor(c => c.Email).NotEmpty().MaximumLength(UsuarioConfiguration.EmailMaxLength);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(100);
    }
}

internal sealed class LoginHandler(
    AppDbContext db,
    TenantContext tenantContext,
    IPasswordHasher<Usuario> passwordHasher,
    ITokenService tokenService,
    TimeProvider timeProvider) : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var tenantId = command.Empresa.Trim().ToLowerInvariant();
        var empresa = await db.Set<Empresa>().AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Activo, cancellationToken);
        if (empresa is null)
        {
            return AutenticacionApi.CredencialesInvalidas;
        }

        using var _ = tenantContext.Use(empresa.TenantId);

        var email = Usuario.NormalizeEmail(command.Email);
        var usuario = await db.Set<Usuario>()
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (usuario is null || !usuario.Activo)
        {
            return AutenticacionApi.CredencialesInvalidas;
        }

        var verificacion = passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, command.Password);
        if (verificacion == PasswordVerificationResult.Failed)
        {
            return AutenticacionApi.CredencialesInvalidas;
        }

        if (verificacion == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.SetPasswordHash(passwordHasher.HashPassword(usuario, command.Password));
        }

        usuario.RegistrarAcceso(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        var permisos = AutenticacionApi.Permisos(usuario);
        var rol = usuario.Rol?.Nombre ?? string.Empty;
        var token = tokenService.Issue(new TokenSubject(usuario.Id, usuario.Email, usuario.Nombre, empresa.TenantId, rol, usuario.EsSuperAdmin, permisos));

        var sesion = new SesionUsuario(usuario.Id, usuario.Email, usuario.Nombre, empresa.TenantId, empresa.RazonSocial, rol, usuario.EsSuperAdmin);
        return new LoginResponse(token.Token, token.ExpiresAt, sesion, permisos);
    }
}

internal sealed class LoginEndpoint : IPublicEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost($"{AutenticacionApi.Route}/login", async (LoginCommand command, ICommandHandler<LoginCommand, LoginResponse> handler, CancellationToken ct) =>
                (await handler.HandleAsync(command, ct)).ToOk())
            .WithName("Login")
            .WithTags(AutenticacionApi.Tag)
            .WithRequestValidation<LoginCommand>()
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireRateLimiting(AutenticacionApi.LoginRateLimit);
}
