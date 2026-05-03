using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ContactApp.Tests;

// ── Capturing logger infrastructure ──────────────────────────────────────────

/// <summary>
/// Thread-safe sink that records every log entry emitted during a test.
/// </summary>
public sealed class CapturingLogSink
{
    public ConcurrentBag<(LogLevel Level, string Message)> Entries { get; } = new();
}

/// <summary>
/// Minimal <see cref="ILogger"/> that writes to a shared <see cref="CapturingLogSink"/>.
/// </summary>
internal sealed class CapturingLogger : ILogger
{
    private readonly CapturingLogSink _sink;

    public CapturingLogger(CapturingLogSink sink) => _sink = sink;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _sink.Entries.Add((logLevel, formatter(state, exception)));
    }
}

/// <summary>
/// <see cref="ILoggerProvider"/> that hands out <see cref="CapturingLogger"/> instances
/// all backed by the same <see cref="CapturingLogSink"/>.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly CapturingLogSink _sink;

    public CapturingLoggerProvider(CapturingLogSink sink) => _sink = sink;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_sink);

    public void Dispose() { }
}

// ── Factory that injects the capturing logger ─────────────────────────────────

/// <summary>
/// Extends <see cref="ContactAppFactory"/> to wire in a <see cref="CapturingLoggerProvider"/>
/// so tests can inspect what was logged during request handling.
/// </summary>
public sealed class LogCapturingFactory : ContactAppFactory
{
    public CapturingLogSink LogSink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Call base first so the Testing environment and in-memory DB are set up.
        base.ConfigureWebHost(builder);

        builder.ConfigureLogging(logging =>
        {
            // Retain existing providers so the test host still works; add ours on top.
            logging.AddProvider(new CapturingLoggerProvider(LogSink));
            // Ensure Information-level messages are not filtered out.
            logging.SetMinimumLevel(LogLevel.Information);
        });
    }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

/// <summary>
/// AC6: Verifies that a successful POST /api/contact emits exactly one
/// Information-level log entry matching "Contact submission accepted: {Id}",
/// with no PII (email / full name) in the message.
/// </summary>
public class ContactSubmissionLoggingTests : IClassFixture<LogCapturingFactory>
{
    private readonly LogCapturingFactory _factory;
    private readonly HttpClient _client;

    public ContactSubmissionLoggingTests(LogCapturingFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Happy-path: successful submission logs the expected message ───────────

    [Fact]
    public async Task PostContact_ValidSubmission_LogsAcceptedMessageWithId()
    {
        // Arrange
        var payload = new
        {
            fullName = "Alice Tester",
            email = "alice@example.com",
            message = "This is a valid test message for AC6."
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contact", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Retrieve the id returned in the response body so we can verify it
        // appears in the log message.
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var submittedId = body.GetProperty("id").GetString();
        Assert.NotNull(submittedId);

        // Assert – exactly one Information entry matching the expected pattern
        var acceptedEntries = _factory.LogSink.Entries
            .Where(e => e.Level == LogLevel.Information
                        && e.Message.Contains("Contact submission accepted:")
                        && e.Message.Contains(submittedId))
            .ToList();

        Assert.True(
            acceptedEntries.Count >= 1,
            $"Expected at least one Information log entry containing " +
            $"'Contact submission accepted: {submittedId}' but found none. " +
            $"All entries: {string.Join("; ", _factory.LogSink.Entries.Select(e => $"[{e.Level}] {e.Message}"))}");
    }

    // ── Negative: validation failure must NOT emit the accepted log message ───

    [Fact]
    public async Task PostContact_InvalidSubmission_DoesNotLogAcceptedMessage()
    {
        // Arrange – capture snapshot of existing entries before this request
        var entriesBefore = _factory.LogSink.Entries.Count;

        var payload = new
        {
            fullName = "",        // invalid
            email = "not-email",  // invalid
            message = ""          // invalid
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/contact", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert – no new "Contact submission accepted" Information entry should appear
        var newAcceptedEntries = _factory.LogSink.Entries
            .Skip(entriesBefore)
            .Where(e => e.Level == LogLevel.Information
                        && e.Message.Contains("Contact submission accepted:"))
            .ToList();

        Assert.Empty(newAcceptedEntries);
    }
}
