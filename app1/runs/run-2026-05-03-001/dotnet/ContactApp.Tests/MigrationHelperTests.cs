using ContactApp.Api.Startup;
using Microsoft.Extensions.Logging;

namespace ContactApp.Tests;

/// <summary>
/// Unit tests for <see cref="MigrationHelper.ApplyMigrationsAsync"/>.
///
/// <para>
/// <c>Database.Migrate()</c> is a non-virtual extension method and cannot be
/// mocked directly.  The production overload of
/// <see cref="MigrationHelper.ApplyMigrationsAsync"/> therefore accepts an
/// <c>Action migrateAction</c> parameter.  Tests pass a plain lambda that
/// counts invocations or throws, making the behaviour fully controllable
/// without a real database provider.
/// </para>
/// </summary>
public class MigrationHelperTests
{
    // ── Minimal ILogger implementation that records LogWarning calls ──────────

    /// <summary>
    /// Captures every log entry emitted at <see cref="LogLevel.Warning"/> or
    /// above so that tests can assert on retry-warning messages without taking
    /// a dependency on a specific logging framework.
    /// </summary>
    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    // ── AC2 (happy path): migration succeeds on the first attempt ─────────────

    /// <summary>
    /// When <paramref name="migrateAction"/> completes without throwing,
    /// <see cref="MigrationHelper.ApplyMigrationsAsync"/> must return normally
    /// and must not emit any warning logs.
    /// </summary>
    [Fact]
    public async Task ApplyMigrationsAsync_SucceedsFirstAttempt_CompletesWithoutException()
    {
        // Arrange
        var logger = new CapturingLogger();
        var callCount = 0;

        void MigrateAction()
        {
            callCount++;
            // succeeds immediately
        }

        // Act
        await MigrationHelper.ApplyMigrationsAsync(
            migrateAction: MigrateAction,
            logger: logger,
            maxAttempts: 5,
            backoffMs: 0);

        // Assert
        Assert.Equal(1, callCount);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    // ── AC2 (negative): transient failure — warns then succeeds ──────────────

    /// <summary>
    /// When <paramref name="migrateAction"/> throws on the first two calls then
    /// succeeds on the third, <see cref="MigrationHelper.ApplyMigrationsAsync"/>
    /// must complete without re-throwing and must have emitted exactly two
    /// <see cref="LogLevel.Warning"/> entries (one per failed attempt).
    /// </summary>
    [Fact]
    public async Task ApplyMigrationsAsync_TransientFailure_RetriesAndWarnsPerFailedAttempt()
    {
        // Arrange
        var logger = new CapturingLogger();
        var callCount = 0;

        void MigrateAction()
        {
            callCount++;
            if (callCount <= 2)
                throw new InvalidOperationException($"Transient failure #{callCount}");
            // succeeds on attempt 3
        }

        // Act
        await MigrationHelper.ApplyMigrationsAsync(
            migrateAction: MigrateAction,
            logger: logger,
            maxAttempts: 5,
            backoffMs: 0);

        // Assert — method completed (no exception thrown)
        Assert.Equal(3, callCount);

        // One warning per failed attempt (attempts 1 and 2).
        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.Message.Contains("1/5"));
        Assert.Contains(warnings, w => w.Message.Contains("2/5"));
    }

    // ── AC3 (negative): all attempts exhausted — exception is re-thrown ────────

    /// <summary>
    /// When <paramref name="migrateAction"/> throws on every attempt,
    /// <see cref="MigrationHelper.ApplyMigrationsAsync"/> must propagate the
    /// exception after exhausting all <c>maxAttempts</c> tries.
    /// Four warnings must be emitted (attempts 1–4) and no warning for attempt 5
    /// because the last exception is re-thrown immediately.
    /// </summary>
    [Fact]
    public async Task ApplyMigrationsAsync_AllAttemptsExhausted_ThrowsAfterMaxAttempts()
    {
        // Arrange
        var logger = new CapturingLogger();
        var callCount = 0;

        void MigrateAction()
        {
            callCount++;
            throw new InvalidOperationException("Persistent failure");
        }

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MigrationHelper.ApplyMigrationsAsync(
                migrateAction: MigrateAction,
                logger: logger,
                maxAttempts: 5,
                backoffMs: 0));

        Assert.Equal(5, callCount);

        // Warnings for attempts 1–4; attempt 5 re-throws without logging.
        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Equal(4, warnings.Count);
    }

    // ── Edge case: single attempt configured — throws immediately ─────────────

    /// <summary>
    /// With <c>maxAttempts = 1</c> there are no retries, so the first exception
    /// must propagate and no warnings should be emitted.
    /// </summary>
    [Fact]
    public async Task ApplyMigrationsAsync_MaxAttemptsOne_ThrowsImmediatelyWithNoWarnings()
    {
        // Arrange
        var logger = new CapturingLogger();
        var callCount = 0;

        void MigrateAction()
        {
            callCount++;
            throw new InvalidOperationException("Instant failure");
        }

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MigrationHelper.ApplyMigrationsAsync(
                migrateAction: MigrateAction,
                logger: logger,
                maxAttempts: 1,
                backoffMs: 0));

        Assert.Equal(1, callCount);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }
}
