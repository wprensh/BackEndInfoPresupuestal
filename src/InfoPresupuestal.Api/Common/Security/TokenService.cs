using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InfoPresupuestal.Api.Common.Security;

public sealed record TokenSubject(
    Guid UserId,
    string Email,
    string Name,
    string TenantId,
    string Role,
    bool IsSuperAdmin,
    IReadOnlyCollection<string> Permissions);

public sealed record IssuedToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    IssuedToken Issue(TokenSubject subject);
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    public IssuedToken Issue(TokenSubject subject)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(jwt.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(AppClaims.UserId, subject.UserId.ToString()),
            new(AppClaims.Email, subject.Email),
            new(AppClaims.Name, subject.Name),
            new(AppClaims.Tenant, subject.TenantId),
            new(AppClaims.Role, subject.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (subject.IsSuperAdmin)
        {
            claims.Add(new Claim(AppClaims.SuperAdmin, "true"));
        }

        claims.AddRange(subject.Permissions.Select(p => new Claim(AppClaims.Permission, p)));

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(SecurityExtensions.CreateKey(jwt.SigningKey), SecurityAlgorithms.HmacSha256));

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
