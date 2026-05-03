using System.Net;
using System.Net.Http.Json;

namespace ContactApp.Tests;

/// <summary>
/// AC4: Verifies CORS preflight behaviour for /api/contact.
///
/// Happy path  — OPTIONS from the allowed origin (http://localhost:5173) must return
///               204 with Access-Control-Allow-Origin and Access-Control-Allow-Methods
///               that includes POST.
///
/// Negative    — OPTIONS from a disallowed origin must NOT receive an
///               Access-Control-Allow-Origin response header.
/// </summary>
public class CorsPreflightTests : IClassFixture<ContactAppFactory>
{
    private readonly HttpClient _client;

    public CorsPreflightTests(ContactAppFactory factory)
    {
        // CreateClient() returns a client that follows redirects by default;
        // we keep defaults – preflight responses are not redirects.
        _client = factory.CreateClient();
    }

    // ── Happy-path preflight ──────────────────────────────────────────────────

    [Fact]
    public async Task Options_AllowedOrigin_Returns204WithCorsHeaders()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/contact");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        // Act
        var response = await _client.SendAsync(request);

        // Assert – status
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert – Access-Control-Allow-Origin must echo the allowed origin
        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins),
            "Expected Access-Control-Allow-Origin header to be present.");
        Assert.Contains("http://localhost:5173", origins);

        // Assert – Access-Control-Allow-Methods must include POST
        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Methods", out var methods),
            "Expected Access-Control-Allow-Methods header to be present.");
        var methodsJoined = string.Join(",", methods);
        Assert.Contains("POST", methodsJoined, StringComparison.OrdinalIgnoreCase);
    }

    // ── Negative preflight ────────────────────────────────────────────────────

    [Fact]
    public async Task Options_DisallowedOrigin_DoesNotReceiveAcaoHeader()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/contact");
        request.Headers.Add("Origin", "http://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        // Act
        var response = await _client.SendAsync(request);

        // Assert – the ACAO header must be absent for disallowed origins
        var hasAcao = response.Headers.Contains("Access-Control-Allow-Origin");
        Assert.False(hasAcao,
            "Disallowed origin must NOT receive an Access-Control-Allow-Origin header.");
    }
}
