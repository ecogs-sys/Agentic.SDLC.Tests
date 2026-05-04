using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ContactApp.Tests;

/// <summary>
/// AC5 smoke test: verifies that the "frontend" CORS policy is registered in DI
/// and contains the expected default origin (http://localhost:5173).
/// </summary>
public class CorsConfigurationTests : IClassFixture<ContactAppFactory>
{
    private readonly ContactAppFactory _factory;

    public CorsConfigurationTests(ContactAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FrontendCorsPolicy_ContainsExpectedOrigin()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var policyProvider = scope.ServiceProvider.GetRequiredService<ICorsPolicyProvider>();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };

        // Act
        var policy = await policyProvider.GetPolicyAsync(httpContext, "frontend");

        // Assert
        Assert.NotNull(policy);
        Assert.Contains("http://localhost:5173", policy.Origins);
    }
}
