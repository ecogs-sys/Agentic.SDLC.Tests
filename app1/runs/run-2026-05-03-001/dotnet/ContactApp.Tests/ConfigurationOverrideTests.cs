using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ContactApp.Api.Data;

namespace ContactApp.Tests;

// ── Factory that overrides the connection-string via a real environment variable

/// <summary>
/// A <see cref="ContactAppFactory"/> variant that:
/// <list type="bullet">
///   <item>Sets the <c>ConnectionStrings__DefaultConnection</c> process environment
///         variable before the host is built, which is picked up by ASP.NET Core's
///         built-in environment-variable configuration provider.</item>
///   <item>Still swaps the DbContext to an in-memory provider so no real
///         PostgreSQL connection is attempted at startup.</item>
///   <item>Cleans up the environment variable in <see cref="Dispose"/>.</item>
/// </list>
/// </summary>
public sealed class EnvVarOverrideFactory : ContactAppFactory
{
    /// <summary>
    /// The connection string injected via the env-var path.
    /// Public so tests can reference it without duplicating the literal.
    /// </summary>
    public const string OverrideConnectionString =
        "Host=override-host;Database=override_db;Username=override_user;Password=s3cr3t";

    private const string EnvVarKey = "ConnectionStrings__DefaultConnection";

    public EnvVarOverrideFactory()
    {
        // Set the real process environment variable BEFORE the WebApplicationFactory
        // builds the host so that ASP.NET Core's env-var provider picks it up.
        Environment.SetEnvironmentVariable(EnvVarKey, OverrideConnectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Call base: sets Environment = "Testing" and replaces DbContext
        // with InMemory so startup migration is skipped and no real PG is needed.
        base.ConfigureWebHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        // Clean up the environment variable so it does not bleed into other tests.
        Environment.SetEnvironmentVariable(EnvVarKey, null);
        base.Dispose(disposing);
    }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

/// <summary>
/// AC1: Verifies that the <c>ConnectionStrings__DefaultConnection</c> environment
/// variable takes precedence over the value defined in appsettings.json, exercising
/// ASP.NET Core's built-in environment-variable configuration provider.
/// </summary>
public class ConfigurationOverrideTests : IClassFixture<EnvVarOverrideFactory>
{
    private readonly EnvVarOverrideFactory _factory;

    public ConfigurationOverrideTests(EnvVarOverrideFactory factory)
    {
        _factory = factory;
    }

    // ── Happy-path: real env var is reflected in IConfiguration ──────────────

    [Fact]
    public void ConnectionStrings_DefaultConnection_ReflectsEnvVarOverride()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // Act
        var resolved = config.GetConnectionString("DefaultConnection");

        // Assert
        Assert.Equal(EnvVarOverrideFactory.OverrideConnectionString, resolved);
    }

    // ── Negative: without the env var override the default from appsettings is used

    [Fact]
    public void ConnectionStrings_DefaultConnection_WithoutOverride_UsesAppsettingsDefault()
    {
        // Arrange – use the standard factory (no env var override injected).
        // The env var must not be set for this test to be valid.
        const string envVarKey = "ConnectionStrings__DefaultConnection";
        var savedValue = Environment.GetEnvironmentVariable(envVarKey);
        Environment.SetEnvironmentVariable(envVarKey, null);

        try
        {
            using var defaultFactory = new ContactAppFactory();
            using var scope = defaultFactory.Services.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            // Act
            var resolved = config.GetConnectionString("DefaultConnection");

            // Assert – the value must NOT equal the test-specific override string,
            // confirming that overrides are isolated to the factory that sets them.
            Assert.NotEqual(EnvVarOverrideFactory.OverrideConnectionString, resolved);
        }
        finally
        {
            // Restore whatever was there before (handles parallel test execution).
            Environment.SetEnvironmentVariable(envVarKey, savedValue);
        }
    }
}
