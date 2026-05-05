using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ContactApp.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace ContactApp.Tests.Controllers;

/// <summary>
/// Integration tests for <see cref="ContactApp.Api.Controllers.ContactController"/>.
///
/// Covers STORY-003 acceptance criteria:
///   AC1  - POST /api/contact accepts application/json.
///   AC2  - No [Authorize] attribute; endpoint is accessible anonymously.
///   AC3  - Valid payload → 201 with { id, receivedAt } and row persisted in DB.
///   AC4  - Invalid payload → 400 with { errors: { camelCaseField: [msg] } }, no row persisted.
///   AC5  - Unhandled exception → 500 with sanitized JSON body (no HTML stack trace).
///   AC6  - Validation error keys are camelCase.
///   AC7a - Integration: POST valid payload returns 201 and row exists in DB.
///   AC7b - Integration: POST invalid payload returns 400 with errors keyed correctly, no row.
///   AC7c - Integration: POST malformed JSON returns 400.
/// </summary>
public sealed class ContactControllerIntegrationTests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // Testcontainer — shared across all tests in the class
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

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");

                builder.ConfigureServices(services =>
                {
                    // Replace the production EF Core DbContext registration with
                    // one pointing at the test Postgres container.
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql(_postgres.GetConnectionString()));
                });
            });

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
    // Helpers
    // -------------------------------------------------------------------------

    private static StringContent ValidJsonPayload(
        string fullName = "Jane Doe",
        string email    = "jane.doe@example.com",
        string phone    = "+1-555-0100",
        string subject  = "Test Subject",
        string message  = "This is a test message for the contact form.")
    {
        var json = JsonSerializer.Serialize(new
        {
            fullName,
            email,
            phone,
            subject,
            message
        });

        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    /// <summary>Returns the count of rows currently in contact_submissions.</summary>
    private async Task<int> GetRowCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ContactSubmissions.CountAsync();
    }

    // =========================================================================
    // AC1 — POST /api/contact accepts application/json
    // =========================================================================

    [Fact]
    public async Task Post_WithApplicationJsonContentType_Returns201()
    {
        // Arrange
        using var content = ValidJsonPayload();
        Assert.Equal("application/json", content.Headers.ContentType!.MediaType);

        // Act
        var response = await _client.PostAsync("/api/contact", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithWrongContentType_Returns415()
    {
        // Arrange — send form-urlencoded instead of JSON
        var content = new StringContent(
            "fullName=Jane&email=jane%40example.com&phone=555&subject=Hi&message=Hello",
            Encoding.UTF8,
            "application/x-www-form-urlencoded");

        // Act
        var response = await _client.PostAsync("/api/contact", content);

        // Assert — endpoint declares [Consumes("application/json")], so wrong type → 415
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    // =========================================================================
    // AC2 — No [Authorize] attribute; endpoint accessible without credentials
    // =========================================================================

    [Fact]
    public async Task Post_WithoutAuthorizationHeader_DoesNotReturn401Or403()
    {
        // Arrange — client has no Authorization header (default)
        using var content = ValidJsonPayload();

        // Act
        var response = await _client.PostAsync("/api/contact", content);

        // Assert
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void ContactController_DoesNotHaveAuthorizeAttribute()
    {
        // Arrange
        var controllerType = typeof(ContactApp.Api.Controllers.ContactController);

        // Act
        var hasAuthorize = controllerType.GetCustomAttributes(
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute),
            inherit: true).Length > 0;

        // Assert
        Assert.False(hasAuthorize, "ContactController must not carry [Authorize].");
    }

    [Fact]
    public void SubmitAction_DoesNotHaveAuthorizeAttribute()
    {
        // Arrange
        var method = typeof(ContactApp.Api.Controllers.ContactController)
            .GetMethod("Submit");
        Assert.NotNull(method);

        // Act
        var hasAuthorize = method!.GetCustomAttributes(
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute),
            inherit: true).Length > 0;

        // Assert
        Assert.False(hasAuthorize, "Submit action must not carry [Authorize].");
    }

    // =========================================================================
    // AC3 — Valid payload → 201 + { id, receivedAt } + row persisted
    // =========================================================================

    [Fact]
    public async Task Post_WithValidPayload_Returns201()
    {
        // Arrange
        using var content = ValidJsonPayload();

        // Act
        var response = await _client.PostAsync("/api/contact", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithValidPayload_ResponseBodyContainsGuidId()
    {
        // Arrange
        using var content = ValidJsonPayload();

        // Act
        var response = await _client.PostAsync("/api/contact", content);
        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body);

        // Assert
        Assert.NotNull(node);
        var idNode = node!["id"];
        Assert.NotNull(idNode);
        var parsed = Guid.TryParse(idNode!.GetValue<string>(), out var id);
        Assert.True(parsed, "Response 'id' must be a valid UUID.");
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Post_WithValidPayload_ResponseBodyContainsReceivedAtAsIso8601Utc()
    {
        // Arrange
        using var content = ValidJsonPayload();
        var beforeCall = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var response = await _client.PostAsync("/api/contact", content);
        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body);

        // Assert
        Assert.NotNull(node);
        var receivedAtNode = node!["receivedAt"];
        Assert.NotNull(receivedAtNode);

        var parsed = DateTime.TryParse(
            receivedAtNode!.GetValue<string>(),
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var receivedAt);

        Assert.True(parsed, "receivedAt must be a parsable date-time string.");
        // Must be UTC
        Assert.Equal(DateTimeKind.Utc, receivedAt.Kind);
        // Must be recent (within 30 seconds of the test run)
        Assert.True(receivedAt >= beforeCall, "receivedAt should be after the test started.");
        Assert.True(receivedAt <= DateTime.UtcNow.AddSeconds(5), "receivedAt should not be in the far future.");
    }

    // =========================================================================
    // AC3 / AC7a — Row persisted in DB after valid submission
    // =========================================================================

    [Fact]
    public async Task Post_WithValidPayload_PersistsRowInDatabase()
    {
        // Arrange
        using var content = ValidJsonPayload(
            fullName: "Integration Test User",
            email: "integration@test.com",
            phone: "1234567890",
            subject: "Persistence Test",
            message: "This row should appear in the database after POST.");

        var countBefore = await GetRowCountAsync();

        // Act
        var response = await _client.PostAsync("/api/contact", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Assert — count increased by exactly one
        var countAfter = await GetRowCountAsync();
        Assert.Equal(countBefore + 1, countAfter);
    }

    [Fact]
    public async Task Post_WithValidPayload_PersistedRowMatchesRequestFields()
    {
        // Arrange
        var uniqueEmail = $"ac3-verify-{Guid.NewGuid():N}@test.com";
        using var content = ValidJsonPayload(
            fullName: "AC3 Verify User",
            email: uniqueEmail,
            phone: "9998887777",
            subject: "Field Verification",
            message: "Checking all fields are stored correctly.");

        // Act
        var response = await _client.PostAsync("/api/contact", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        var responseNode = JsonNode.Parse(responseBody)!;
        var returnedId = Guid.Parse(responseNode["id"]!.GetValue<string>());

        // Assert — query DB directly for the returned id
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.ContactSubmissions.FindAsync(returnedId);

        Assert.NotNull(row);
        Assert.Equal("AC3 Verify User", row!.FullName);
        Assert.Equal(uniqueEmail, row.Email);
        Assert.Equal("9998887777", row.Phone);
        Assert.Equal("Field Verification", row.Subject);
        Assert.Equal("Checking all fields are stored correctly.", row.Message);
        Assert.Equal(DateTimeKind.Utc, DateTime.SpecifyKind(row.ReceivedAt, DateTimeKind.Utc).Kind);
    }

    // =========================================================================
    // AC4 / AC7b — Invalid payload → 400 + errors dict + no row persisted
    // =========================================================================

    [Fact]
    public async Task Post_WithEmptyFullName_Returns400()
    {
        // Arrange
        using var content = ValidJsonPayload(fullName: "");

        // Act
        var response = await _client.PostAsync("/api/contact", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithEmptyEmail_Returns400()
    {
        // Arrange
        using var content = ValidJsonPayload(email: "");

        // Act
        var response = await _client.PostAsync("/api/contact", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithInvalidEmailFormat_Returns400()
    {
        // Arrange
        using var content = ValidJsonPayload(email: "not-a-valid-email");

        // Act
        var response = await _client.PostAsync("/api/contact", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithInvalidPayload_ResponseBodyHasErrorsProperty()
    {
        // Arrange
        using var content = ValidJsonPayload(fullName: "", email: "");

        // Act
        var response = await _client.PostAsync("/api/contact", content);
        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body);

        // Assert
        Assert.NotNull(node);
        Assert.NotNull(node!["errors"]);
    }

    [Fact]
    public async Task Post_WithInvalidPayload_ErrorsPropertyIsAnObject()
    {
        // Arrange
        using var content = ValidJsonPayload(fullName: "");

        // Act
        var response = await _client.PostAsync("/api/contact", content);
        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body);

        // Assert
        var errorsNode = node!["errors"];
        Assert.NotNull(errorsNode);
        Assert.IsType<JsonObject>(errorsNode);
    }

    [Fact]
    public async Task Post_WithEmptyFullName_ErrorsContainsFullNameKey()
    {
        // Arrange
        using var content = ValidJsonPayload(fullName: "");

        // Act
        var response = await _client.PostAsync("/api/contact", content);
        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body)!;

        // Assert
        var errors = node["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("fullName"), "Errors should contain 'fullName' key.");
    }

    [Fact]
    public async Task Post_WithEmptyEmail_ErrorsContainsEmailKey()
    {
        // Arrange
        using var content = ValidJsonPayload(email: "");

        // Act
        var response = await _client.PostAsync("/api/contact", content);
        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body)!;

        // Assert
        var errors = node["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("email"), "Errors should contain 'email' key.");
    }

    // =========================================================================
    // AC4 — No row persisted when validation fails
    // =========================================================================

    [Fact]
    public async Task Post_WithInvalidPayload_DoesNotPersistRowInDatabase()
    {
        // Arrange
        using var content = ValidJsonPayload(fullName: "");

        var countBefore = await GetRowCountAsync();

        // Act
        var response = await _client.PostAsync("/api/contact", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — count unchanged
        var countAfter = await GetRowCountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    // =========================================================================
    // AC6 — Error keys are camelCase
    // =========================================================================

    [Theory]
    [InlineData("fullName", "")]
    [InlineData("email", "")]
    [InlineData("phone", "")]
    [InlineData("subject", "")]
    [InlineData("message", "")]
    public async Task Post_WithOneEmptyField_ErrorKeyIsCamelCase(string fieldName, string emptyValue)
    {
        // Arrange — build payload with the specific field empty
        var payloadObj = new Dictionary<string, string>
        {
            ["fullName"] = "Jane Doe",
            ["email"]    = "jane@example.com",
            ["phone"]    = "+1-555-0100",
            ["subject"]  = "Test Subject",
            ["message"]  = "This is a test message."
        };
        payloadObj[fieldName] = emptyValue;

        var json = JsonSerializer.Serialize(payloadObj);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/contact", content);
        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body)!;

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = node["errors"]!.AsObject();
        Assert.True(errors.ContainsKey(fieldName),
            $"Error key '{fieldName}' should be camelCase in the response. Body: {body}");
    }

    [Fact]
    public async Task Post_WithInvalidPayload_ErrorValuesAreArraysOfStrings()
    {
        // Arrange
        using var content = ValidJsonPayload(fullName: "");

        // Act
        var response = await _client.PostAsync("/api/contact", content);
        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body)!;

        // Assert
        var errorsObj = node["errors"]!.AsObject();
        foreach (var kvp in errorsObj)
        {
            var arrayNode = kvp.Value as JsonArray;
            Assert.NotNull(arrayNode);
            Assert.True(arrayNode!.Count > 0, $"Error array for '{kvp.Key}' should not be empty.");
            foreach (var item in arrayNode)
            {
                Assert.NotNull(item);
                Assert.IsAssignableFrom<JsonValue>(item);
            }
        }
    }

    [Fact]
    public async Task Post_WithMultipleInvalidFields_ErrorsContainsMultipleCamelCaseKeys()
    {
        // Arrange — all fields empty
        var json = JsonSerializer.Serialize(new
        {
            fullName = "",
            email    = "",
            phone    = "",
            subject  = "",
            message  = ""
        });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/contact", content);
        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body)!;

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = node["errors"]!.AsObject();
        Assert.True(errors.Count >= 5, "All 5 fields empty should produce 5 error keys.");

        // All keys should start with a lowercase letter (camelCase)
        foreach (var kvp in errors)
        {
            Assert.True(char.IsLower(kvp.Key[0]),
                $"Error key '{kvp.Key}' should start with a lowercase letter (camelCase).");
        }
    }

    // =========================================================================
    // AC5 — Unhandled exception → 500 with sanitized JSON, no HTML stack trace
    // =========================================================================

    [Fact]
    public async Task Post_WhenDbThrowsUnhandledException_Returns500()
    {
        // Arrange — create a separate factory where the DbContext is replaced
        // with a version that throws on SaveChangesAsync.
        await using var throwingFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");

                builder.ConfigureServices(services =>
                {
                    // Replace DbContext with one backed by a deliberately broken
                    // in-memory provider that will throw during SaveChanges.
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();

                    // Use an invalid connection string to force an exception at
                    // the point of SaveChangesAsync (connection refused).
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql("Host=localhost;Port=1;Database=none;Username=nobody;Password=wrong"));
                });
            });

        using var throwingClient = throwingFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var content = ValidJsonPayload();

        // Act
        var response = await throwingClient.PostAsync("/api/contact", content);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Post_WhenDbThrowsUnhandledException_ResponseBodyIsJsonNotHtml()
    {
        // Arrange
        await using var throwingFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql("Host=localhost;Port=1;Database=none;Username=nobody;Password=wrong"));
                });
            });

        using var throwingClient = throwingFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var content = ValidJsonPayload();

        // Act
        var response = await throwingClient.PostAsync("/api/contact", content);
        var body = await response.Content.ReadAsStringAsync();

        // Assert — body must not contain HTML
        Assert.False(body.TrimStart().StartsWith("<"),
            $"Response body must not be HTML. Received: {body[..Math.Min(200, body.Length)]}");

        // Assert — body must be valid JSON
        JsonNode? parsed = null;
        var exception = Record.Exception(() => parsed = JsonNode.Parse(body));
        Assert.Null(exception);
        Assert.NotNull(parsed);
    }

    [Fact]
    public async Task Post_WhenDbThrowsUnhandledException_SanitizedBodyContainsNoStackTrace()
    {
        // Arrange
        await using var throwingFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql("Host=localhost;Port=1;Database=none;Username=nobody;Password=wrong"));
                });
            });

        using var throwingClient = throwingFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var content = ValidJsonPayload();

        // Act
        var response = await throwingClient.PostAsync("/api/contact", content);
        var body = await response.Content.ReadAsStringAsync();

        // Assert — body must not contain stack-trace markers
        Assert.DoesNotContain("at ContactApp", body);
        Assert.DoesNotContain("System.Exception", body);
        Assert.DoesNotContain("   at ", body);
    }

    // =========================================================================
    // AC7b — Integration: POST invalid returns 400 with errors keyed correctly, no row
    // =========================================================================

    [Fact]
    public async Task Integration_PostInvalidPayload_Returns400WithCorrectErrorShape()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new
        {
            fullName = "",
            email    = "not-an-email",
            phone    = "+1-555-0100",
            subject  = "Test Subject",
            message  = "Test message content."
        });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/contact", content);
        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body)!;

        // Assert status
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert shape: { errors: { fullName: [...], email: [...] } }
        var errors = node["errors"];
        Assert.NotNull(errors);

        var errorsObj = errors!.AsObject();
        Assert.True(errorsObj.ContainsKey("fullName"),
            $"Expected 'fullName' key in errors. Body: {body}");
        Assert.True(errorsObj.ContainsKey("email"),
            $"Expected 'email' key in errors. Body: {body}");

        // Each error value must be a non-empty array
        foreach (var kvp in errorsObj)
        {
            var arr = kvp.Value as JsonArray;
            Assert.NotNull(arr);
            Assert.True(arr!.Count > 0, $"Error array for '{kvp.Key}' must not be empty.");
        }
    }

    [Fact]
    public async Task Integration_PostInvalidPayload_DoesNotPersistAnyRow()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new
        {
            fullName = "",
            email    = "invalid-email",
            phone    = "",
            subject  = "",
            message  = ""
        });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var countBefore = await GetRowCountAsync();

        // Act
        await _client.PostAsync("/api/contact", content);

        // Assert
        var countAfter = await GetRowCountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    // =========================================================================
    // AC7c — Integration: POST malformed JSON returns 400
    // =========================================================================

    [Fact]
    public async Task Integration_PostMalformedJson_Returns400()
    {
        // Arrange — deliberately malformed JSON
        using var content = new StringContent(
            "{ this is not valid JSON }",
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/contact", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Integration_PostTruncatedJson_Returns400()
    {
        // Arrange — JSON that is cut off mid-way
        using var content = new StringContent(
            "{\"fullName\":\"Jane\"",
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/contact", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Integration_PostEmptyBody_Returns400()
    {
        // Arrange — empty body with JSON content type
        using var content = new StringContent(
            string.Empty,
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/contact", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // =========================================================================
    // AC7a — Integration: POST valid payload returns 201 and row exists
    // =========================================================================

    [Fact]
    public async Task Integration_PostValidPayload_Returns201AndRowExistsInDb()
    {
        // Arrange
        var uniqueEmail = $"ac7a-{Guid.NewGuid():N}@integration.test";
        using var content = ValidJsonPayload(
            fullName: "AC7a Integration User",
            email: uniqueEmail,
            phone: "0000000001",
            subject: "AC7a Subject",
            message: "AC7a integration test — row must exist in DB after 201.");

        // Act
        var response = await _client.PostAsync("/api/contact", content);

        // Assert status
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Assert row in DB
        var responseBody = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(responseBody)!;
        var returnedId = Guid.Parse(node["id"]!.GetValue<string>());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.ContactSubmissions.FindAsync(returnedId);

        Assert.NotNull(row);
        Assert.Equal("AC7a Integration User", row!.FullName);
        Assert.Equal(uniqueEmail, row.Email);
    }
}
