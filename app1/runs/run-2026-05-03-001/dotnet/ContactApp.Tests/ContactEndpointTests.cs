using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ContactApp.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContactApp.Tests;

// ── Factory helpers ────────────────────────────────────────────────────────────

/// <summary>
/// Removes every service descriptor that was registered by the production
/// <c>AddDbContext&lt;ContactDbContext&gt;</c> call, including provider-specific
/// option-configuration callbacks (<see cref="IDbContextOptionsConfiguration{T}"/>).
/// This prevents the "multiple database providers" InvalidOperationException that
/// occurs when the Npgsql provider and the InMemory provider are both registered
/// in the same service provider.
/// </summary>
internal static class DbContextServiceRemover
{
    public static void RemoveContactDbContextServices(IServiceCollection services)
    {
        // Remove resolved-options cache and the context itself.
        services.RemoveAll<DbContextOptions<ContactDbContext>>();
        services.RemoveAll<ContactDbContext>();

        // Remove every IDbContextOptionsConfiguration<ContactDbContext> descriptor
        // that the production AddDbContext call registered (e.g. the Npgsql lambda).
        var optCfgType = typeof(IDbContextOptionsConfiguration<ContactDbContext>);
        var toRemove = services.Where(d => d.ServiceType == optCfgType).ToList();
        foreach (var d in toRemove)
            services.Remove(d);
    }
}

/// <summary>
/// Standard factory: replaces PostgreSQL with an in-memory EF Core provider.
/// The startup migration block is bypassed because the Testing environment
/// skips the <c>db.Database.Migrate()</c> call (see Program.cs).
/// </summary>
public class ContactAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            DbContextServiceRemover.RemoveContactDbContextServices(services);

            // Register an in-memory replacement keyed by a unique DB name per factory instance.
            services.AddDbContext<ContactDbContext>(options =>
                options.UseInMemoryDatabase("ContactTestDb_" + Guid.NewGuid()));
        });
    }
}

/// <summary>
/// Factory variant that injects a <see cref="FailingContactDbContext"/> so that
/// every call to SaveChangesAsync throws, simulating an unexpected DB error.
/// </summary>
public class FailingDbFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            DbContextServiceRemover.RemoveContactDbContextServices(services);

            // Register a context that throws on save.
            services.AddDbContext<ContactDbContext, FailingContactDbContext>(options =>
                options.UseInMemoryDatabase("FailingDb_" + Guid.NewGuid()));
        });
    }
}

/// <summary>Subclass that always throws from SaveChangesAsync.</summary>
public class FailingContactDbContext : ContactDbContext
{
    public FailingContactDbContext(DbContextOptions<ContactDbContext> options)
        : base(options) { }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Simulated database failure.");
}

// ── Test class ─────────────────────────────────────────────────────────────────

public class ContactEndpointTests : IClassFixture<ContactAppFactory>
{
    private readonly HttpClient _client;
    private readonly ContactAppFactory _factory;

    public ContactEndpointTests(ContactAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── AC7: GET /api/health ───────────────────────────────────────────────────

    [Fact]
    public async Task Health_ReturnsOkWithStatusOk()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    // ── AC1: Valid submission → 201 with id and receivedAt ────────────────────

    [Fact]
    public async Task PostContact_ValidBody_Returns201WithIdAndReceivedAt()
    {
        var payload = new
        {
            fullName = "Jane Doe",
            email = "jane@example.com",
            message = "Hello, this is a test message."
        };

        var response = await _client.PostAsJsonAsync("/api/contact", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // id must be a non-empty guid.
        var idString = body.GetProperty("id").GetString();
        Assert.True(Guid.TryParse(idString, out var parsedId));
        Assert.NotEqual(Guid.Empty, parsedId);

        // receivedAt must be a parseable ISO-8601 datetime.
        var receivedAtString = body.GetProperty("receivedAt").GetString();
        Assert.True(DateTime.TryParse(receivedAtString, out _),
            $"receivedAt '{receivedAtString}' is not a valid ISO-8601 datetime.");
    }

    // ── AC2: Empty fullName → 400 with errors.fullName ────────────────────────

    [Fact]
    public async Task PostContact_EmptyFullName_Returns400WithFullNameError()
    {
        var payload = new
        {
            fullName = "",
            email = "jane@example.com",
            message = "Valid message."
        };

        var response = await _client.PostAsJsonAsync("/api/contact", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out var errors),
            "Response body must contain an 'errors' property.");
        Assert.True(errors.TryGetProperty("fullName", out _),
            "errors must contain 'fullName'.");
    }

    // ── AC3: Invalid email → 400 with errors.email ────────────────────────────

    [Fact]
    public async Task PostContact_InvalidEmail_Returns400WithEmailError()
    {
        var payload = new
        {
            fullName = "Jane Doe",
            email = "not-an-email",
            message = "Valid message."
        };

        var response = await _client.PostAsJsonAsync("/api/contact", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out var errors),
            "Response body must contain an 'errors' property.");
        Assert.True(errors.TryGetProperty("email", out _),
            "errors must contain 'email'.");
    }

    // ── AC4: Message > 5000 chars → 400 with errors.message ──────────────────

    [Fact]
    public async Task PostContact_MessageTooLong_Returns400WithMessageError()
    {
        var payload = new
        {
            fullName = "Jane Doe",
            email = "jane@example.com",
            message = new string('x', 5001)
        };

        var response = await _client.PostAsJsonAsync("/api/contact", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out var errors),
            "Response body must contain an 'errors' property.");
        Assert.True(errors.TryGetProperty("message", out _),
            "errors must contain 'message'.");
    }

    // ── AC5: Multiple invalid fields → 400 with all offending fields ──────────

    [Fact]
    public async Task PostContact_MultipleInvalidFields_Returns400WithAllErrors()
    {
        var payload = new
        {
            fullName = "",
            email = "not-an-email",
            message = ""
        };

        var response = await _client.PostAsJsonAsync("/api/contact", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out var errors),
            "Response body must contain an 'errors' property.");
        Assert.True(errors.TryGetProperty("fullName", out _),
            "errors must contain 'fullName'.");
        Assert.True(errors.TryGetProperty("email", out _),
            "errors must contain 'email'.");
        Assert.True(errors.TryGetProperty("message", out _),
            "errors must contain 'message'.");
    }

    // ── AC6: DB throws → 500 with safe error message ──────────────────────────

    [Fact]
    public async Task PostContact_WhenDbThrows_Returns500WithErrorMessage()
    {
        using var failingFactory = new FailingDbFactory();
        var client = failingFactory.CreateClient();

        var payload = new
        {
            fullName = "Jane Doe",
            email = "jane@example.com",
            message = "This should trigger a DB failure."
        };

        var response = await client.PostAsJsonAsync("/api/contact", payload);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out var errorProp),
            "Response body must contain an 'error' property.");
        Assert.Equal("An unexpected error occurred.", errorProp.GetString());
    }

    // ── AC9: No UseAuthentication / UseAuthorization in pipeline ──────────────

    [Fact]
    public void Pipeline_DoesNotRegisterAuthenticationOrAuthorizationServices()
    {
        // The absence of UseAuthentication / UseAuthorization is observable via
        // the DI container: those middleware extensions register their core
        // services (IAuthenticationService / IAuthorizationService) when called.
        // If neither service is present the middleware was never added.
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var authService = sp.GetService<IAuthenticationService>();
        Assert.Null(authService);

        var authzService = sp.GetService<IAuthorizationService>();
        Assert.Null(authzService);
    }
}
