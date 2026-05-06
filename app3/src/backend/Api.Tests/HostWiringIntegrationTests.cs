using System.Net;
using System.Text;
using System.Text.Json;
using ContactApp.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace ContactApp.Tests;

/// <summary>
/// Integration tests covering STORY-004 acceptance criteria for host wiring:
///   AC3  - CORS policy "FrontendPolicy" allows http://localhost:3000 and blocks unknown origins.
///   AC4  - GET /healthz returns HTTP 200.
///   AC5  - No 401/403 returned on valid requests (no auth middleware).
///   AC7  - Swagger available in Development, not available in Production.
///   AC10 - Smoke tests for /healthz and CORS.
/// </summary>
public sealed class HostWiringIntegrationTests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // Testcontainer — shared across all tests in the class (except Swagger tests
    // which use their own ephemeral factories)
    // -------------------------------------------------------------------------

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    // -------------------------------------------------------------------------
    // IAsyncLifetime — lifecycle
    // -------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = BuildFactory("Test");
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // Factory helper
    // -------------------------------------------------------------------------

    private WebApplicationFactory<Program> BuildFactory(string environment)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql(_postgres.GetConnectionString()));
                });
            });
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static StringContent ValidJsonPayload() =>
        new StringContent(
            JsonSerializer.Serialize(new
            {
                fullName = "Jane Doe",
                email    = "jane.doe@example.com",
                phone    = "+1-555-0100",
                subject  = "Test Subject",
                message  = "This is a test message for the contact form."
            }),
            Encoding.UTF8,
            "application/json");

    // =========================================================================
    // AC4 / AC10 — GET /healthz returns HTTP 200
    // =========================================================================

    [Fact]
    public async Task Get_Healthz_Returns200()
    {
        // Arrange
        // (factory and client already initialised via InitializeAsync)

        // Act
        var response = await _client.GetAsync("/healthz");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_Healthz_DoesNotReturn404()
    {
        // Arrange / Act
        var response = await _client.GetAsync("/healthz");

        // Assert — endpoint must be mapped
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // AC3 / AC10 — CORS preflight: allowed origin returns CORS headers
    // =========================================================================

    [Fact]
    public async Task Options_ApiContact_WithFrontendOrigin_ReturnsCorsHeaders()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/contact");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — status 200 or 204 for a preflight
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NoContent,
            $"Expected 200 or 204 for CORS preflight, got {(int)response.StatusCode}.");

        // Assert — CORS allow-origin header present with the requested origin
        var corsHeader = response.Headers.Contains("Access-Control-Allow-Origin")
            ? string.Join(",", response.Headers.GetValues("Access-Control-Allow-Origin"))
            : null;

        Assert.NotNull(corsHeader);
        Assert.Equal("http://localhost:3000", corsHeader);
    }

    [Fact]
    public async Task Options_ApiContact_WithFrontendOrigin_ReturnsAllowMethodContainingPost()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/contact");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — preflight must advertise POST as an allowed method
        var allowMethods = response.Headers.Contains("Access-Control-Allow-Methods")
            ? string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods"))
            : string.Empty;

        Assert.Contains("POST", allowMethods, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // AC3 — CORS preflight: unknown origin does NOT get CORS header
    // =========================================================================

    [Fact]
    public async Task Options_ApiContact_WithUnknownOrigin_DoesNotReturnCorsHeader()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/contact");
        request.Headers.Add("Origin", "http://malicious.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — Access-Control-Allow-Origin must not be present (or must NOT echo back the origin)
        var hasCorsHeader = response.Headers.Contains("Access-Control-Allow-Origin");
        if (hasCorsHeader)
        {
            var corsHeader = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Origin"));
            Assert.NotEqual("http://malicious.example.com", corsHeader);
        }
        // If the header is absent entirely, that also satisfies the requirement.
    }

    // =========================================================================
    // AC3 / AC10 — Actual POST with allowed origin echoes CORS header
    // =========================================================================

    [Fact]
    public async Task Post_ApiContact_WithFrontendOrigin_ResponseIncludesCorsHeader()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/contact");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Content = ValidJsonPayload();

        // Act
        var response = await _client.SendAsync(request);

        // Assert — CORS allow-origin must be echoed on the actual response
        var corsHeader = response.Headers.Contains("Access-Control-Allow-Origin")
            ? string.Join(",", response.Headers.GetValues("Access-Control-Allow-Origin"))
            : null;

        Assert.NotNull(corsHeader);
        Assert.Equal("http://localhost:3000", corsHeader);
    }

    [Fact]
    public async Task Post_ApiContact_WithFrontendOrigin_Returns201()
    {
        // Arrange — valid payload from the allowed origin must succeed
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/contact");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Content = ValidJsonPayload();

        // Act
        var response = await _client.SendAsync(request);

        // Assert — no CORS rejection; endpoint still returns 201
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // =========================================================================
    // AC5 — No auth: valid requests never return 401 or 403
    // =========================================================================

    [Fact]
    public async Task Post_ApiContact_WithNoAuthorizationHeader_DoesNotReturn401Or403()
    {
        // Arrange — no Authorization header on client (default)
        using var content = ValidJsonPayload();

        // Act
        var response = await _client.PostAsync("/api/contact", content);

        // Assert
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_Healthz_WithNoAuthorizationHeader_DoesNotReturn401Or403()
    {
        // Arrange / Act
        var response = await _client.GetAsync("/healthz");

        // Assert
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // =========================================================================
    // AC7 — Swagger available in Development, NOT available in Production
    // =========================================================================

    [Fact]
    public async Task Get_Swagger_InDevelopment_Returns200()
    {
        // Arrange — factory configured with Development environment
        await using var devFactory = BuildFactory("Development");
        using var devClient = devFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await devClient.GetAsync("/openapi/v1.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_Swagger_InProduction_Returns404()
    {
        // Arrange — factory configured with Production environment
        await using var prodFactory = BuildFactory("Production");
        using var prodClient = prodFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await prodClient.GetAsync("/openapi/v1.json");

        // Assert — endpoint is not mapped in Production
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // AC10 — Smoke: /healthz and CORS both functional in one test class
    // =========================================================================

    [Fact]
    public async Task Smoke_HealthzAndCors_BothFunctionalTogether()
    {
        // -- healthz smoke --
        var healthResponse = await _client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        // -- CORS smoke (preflight) --
        using var corsRequest = new HttpRequestMessage(HttpMethod.Options, "/api/contact");
        corsRequest.Headers.Add("Origin", "http://localhost:3000");
        corsRequest.Headers.Add("Access-Control-Request-Method", "POST");
        corsRequest.Headers.Add("Access-Control-Request-Headers", "Content-Type");
        var corsResponse = await _client.SendAsync(corsRequest);

        Assert.True(
            corsResponse.StatusCode == HttpStatusCode.OK ||
            corsResponse.StatusCode == HttpStatusCode.NoContent,
            $"Smoke CORS preflight status: {(int)corsResponse.StatusCode}");

        Assert.True(
            corsResponse.Headers.Contains("Access-Control-Allow-Origin"),
            "Smoke: CORS preflight must return Access-Control-Allow-Origin header.");
    }
}
