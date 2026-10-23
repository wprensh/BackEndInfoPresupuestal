using InfoPresupuestal.Api.Common.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace InfoPresupuestal.Api.Tests.Common.MultiTenancy;

public sealed class TenantResolutionFilterTests
{
    private readonly TenantContext _tenant = new();
    private readonly TenantResolutionFilter _filter;

    public TenantResolutionFilterTests()
    {
        var options = new MultiTenancyOptions { Tenants = [new TenantDefinition { Id = "acme" }] };
        _filter = new TenantResolutionFilter(
            _tenant,
            new ConfigurationTenantStore(new StaticOptionsMonitor(options)),
            Options.Create(options));
    }

    [Theory]
    [InlineData(null, StatusCodes.Status400BadRequest)]
    [InlineData("otro", StatusCodes.Status404NotFound)]
    [InlineData("ACME", StatusCodes.Status404NotFound)]
    [InlineData("acme:x", StatusCodes.Status404NotFound)]
    public async Task Rejects_missing_or_unknown_tenant(string? header, int expectedStatus)
    {
        var (result, nextCalled) = await InvokeAsync(header);

        Assert.False(nextCalled);
        Assert.Equal(expectedStatus, Assert.IsType<ProblemHttpResult>(result).StatusCode);
        Assert.False(_tenant.HasTenant);
    }

    [Fact]
    public async Task Sets_tenant_and_continues_when_valid()
    {
        var (_, nextCalled) = await InvokeAsync("acme");

        Assert.True(nextCalled);
        Assert.Equal("acme", _tenant.TenantId);
    }

    private async Task<(object? Result, bool NextCalled)> InvokeAsync(string? header)
    {
        var http = new DefaultHttpContext();
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

    private sealed class StaticOptionsMonitor(MultiTenancyOptions value) : IOptionsMonitor<MultiTenancyOptions>
    {
        public MultiTenancyOptions CurrentValue => value;

        public MultiTenancyOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<MultiTenancyOptions, string?> listener) => null;
    }
}
