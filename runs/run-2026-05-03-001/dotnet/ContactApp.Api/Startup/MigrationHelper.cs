namespace ContactApp.Api.Startup;

/// <summary>
/// Encapsulates the startup migration retry loop so that it can be tested
/// independently of the ASP.NET Core hosting infrastructure.
/// </summary>
public static class MigrationHelper
{
    /// <summary>
    /// Runs <paramref name="migrateAction"/> up to <c>maxAttempts</c> times,
    /// logging a warning after each transient failure and re-throwing on the
    /// final attempt.
    /// </summary>
    /// <param name="migrateAction">
    ///   The action that performs the actual migration (e.g.
    ///   <c>() =&gt; db.Database.Migrate()</c>).  Accepting a delegate instead
    ///   of calling <c>db.Database.Migrate()</c> directly makes the method
    ///   testable without requiring a real database provider.
    /// </param>
    /// <param name="logger">Logger used to emit retry warnings.</param>
    /// <param name="maxAttempts">Maximum number of attempts (default 5).</param>
    /// <param name="backoffMs">Milliseconds to wait between attempts (default 2000).</param>
    public static async Task ApplyMigrationsAsync(
        Action migrateAction,
        ILogger logger,
        int maxAttempts = 5,
        int backoffMs = 2_000)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                migrateAction();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex,
                    "Migration attempt {Attempt}/{Max} failed; retrying in {Ms}ms.",
                    attempt, maxAttempts, backoffMs);
                await Task.Delay(backoffMs);
            }
        }
    }
}
