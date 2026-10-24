using System.Security.Claims;
using InfoPresupuestal.Api.Common.MultiTenancy;
using InfoPresupuestal.Api.Common.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace InfoPresupuestal.Api.Tests.Common.MultiTenancy;

public sealed class TenantResolutionFilterTests
{
    private readonly TenantContext _tenant = new();
    private readonly TenantResolutionFilter _filter;

    public TenantResolutionFilterTests() =>
        _filter = new TenantResolutionFilter(_tenant, new FakeTenantStore("acme", "demo", "sistema"), Options.Create(new MultiTenancyOptions()));

    [Fact]
    public async Task Uses_the_tenant_claim_of_the_token()
    {
        var (_, nextCalled) = await InvokeAsync(User("acme"));

        Assert.True(nextCalled);
        Assert.Equal("acme", _tenant.TenantId);
    }

    [Fact]
    public async Task A_normal_user_cannot_switch_tenant_with_the_header()
    {
        var (_, nextCalled) = await InvokeAsync(User("acme"), header: "demo");

        Assert.True(nextCalled);
        Assert.Equal("acme", _tenant.TenantId);
    }

    [Fact]
    public async Task The_superadmin_can_act_on_another_tenant_with_the_header()
    {
        var (_, nextCalled) = await InvokeAsync(User("sistema", superAdmin: true), header: "demo");

        Assert.True(nextCalled);
        Assert.Equal("demo", _tenant.TenantId);
    }

    [Theory]
    [InlineData(null, StatusCodes.Status401Unauthorized)]
    [InlineData("inactiva", StatusCodes.Status403Forbidden)]
    [InlineData("ACME", StatusCodes.Status403Forbidden)]
    [InlineData("acme:x", StatusCodes.Status403Forbidden)]
    public async Task Rejects_missing_unknown_or_inactive_tenant(string? tenantClaim, int expectedStatus)
    {
        var (result, nextCalled) = await InvokeAsync(User(tenantClaim));

        Assert.False(nextCalled);
        Assert.Equal(expectedStatus, Assert.IsType<ProblemHttpResult>(result).StatusCode);
        Assert.False(_tenant.HasTenant);
    }

    private static ClaimsPrincipal User(string? tenant, bool superAdmin = false)
    {
        var claims = new List<Claim> { new(AppClaims.UserId, Guid.NewGuid().ToString()) };
        if (tenant is not null)
        {
            claims.Add(new Claim(AppClaims.Tenant, tenant));
        }

        if (superAdmin)
        {
            claims.Add(new Claim(AppClaims.SuperAdmin, "true"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private async Task<(object? Result, bool NextCalled)> InvokeAsync(ClaimsPrincipal user, string? header = null)
    {
        var http = new DefaultHttpContext { User = user };
        if (header is not null)
        {
            http.Request.Headers["X-Tenant-Id"] = header;
        }

        var nextCalled = false;
        var result = await _filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(http), _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        return (result, nextCalled);
    }

    private sealed class FakeTenantStore(params string[] activos) : ITenantStore
    {
        public Task<bool> ExistsAsync(string tenantId, CancellationToken cancellationToken) => Task.FromResult(activos.Contains(tenantId));
    }
}
