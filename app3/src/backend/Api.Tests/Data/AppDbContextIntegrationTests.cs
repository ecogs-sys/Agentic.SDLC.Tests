using ContactApp.Api.Data;
using ContactApp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ContactApp.Tests.Data;

/// <summary>
/// Integration tests for <see cref="AppDbContext"/> backed by a real Postgres instance
/// running in a Testcontainer.
///
/// Covers STORY-001 acceptance criteria:
///   AC1 - ContactSubmission entity round-trips all fields through Postgres correctly.
///   AC2 - MigrateAsync() against a clean database creates the contact_submissions table
///         with correct snake_case column names.
///   AC3 - A ContactSubmission row can be inserted and retrieved with all fields intact.
///   AC4 - The received_at index exists after migration.
/// </summary>
public sealed class AppDbContextIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private AppDbContext _db = null!;

    // -------------------------------------------------------------------------
    // IAsyncLifetime — container lifecycle
    // -------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _db = new AppDbContext(options);

        // Run migrations so all tests share a fully migrated schema.
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ContactSubmission BuildSubmission(
        string fullName  = "Jane Doe",
        string email     = "jane@example.com",
        string phone     = "+1-555-0100",
        string subject   = "Hello",
        string message   = "Test message body",
        DateTime? receivedAt = null)
    {
        return new ContactSubmission
        {
            Id         = Guid.NewGuid(),
            FullName   = fullName,
            Email      = email,
            Phone      = phone,
            Subject    = subject,
            Message    = message,
            ReceivedAt = DateTime.SpecifyKind(
                receivedAt ?? new DateTime(2026, 5, 4, 12, 0, 0),
                DateTimeKind.Utc)
        };
    }

    // =========================================================================
    // AC2 — MigrateAsync creates the contact_submissions table with correct
    //        snake_case column names.
    // =========================================================================

    [Fact]
    public async Task MigrateAsync_OnCleanDatabase_CreatesContactSubmissionsTable()
    {
        // Arrange — migration already ran in InitializeAsync.
        // Query the information_schema to verify the table exists.
        var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_name = 'contact_submissions';";

        // Act
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        await conn.CloseAsync();

        // Assert
        Assert.Equal(1L, count);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("full_name")]
    [InlineData("email")]
    [InlineData("phone")]
    [InlineData("subject")]
    [InlineData("message")]
    [InlineData("received_at")]
    public async Task MigrateAsync_OnCleanDatabase_CreatesExpectedSnakeCaseColumns(string columnName)
    {
        // Arrange — migration already ran in InitializeAsync.
        var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM information_schema.columns " +
            "WHERE table_schema = 'public' " +
            "  AND table_name   = 'contact_submissions' " +
            $" AND column_name  = '{columnName}';";

        // Act
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        await conn.CloseAsync();

        // Assert
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task MigrateAsync_OnCleanDatabase_DoesNotCreateUnexpectedTable()
    {
        // Negative: a table that was never defined should not exist.
        var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_name = 'nonexistent_table';";

        var count = (long)(await cmd.ExecuteScalarAsync())!;
        await conn.CloseAsync();

        Assert.Equal(0L, count);
    }

    // =========================================================================
    // AC4 — The received_at index exists after migration.
    // =========================================================================

    [Fact]
    public async Task MigrateAsync_OnCleanDatabase_CreatesReceivedAtIndex()
    {
        // Arrange — migration already ran in InitializeAsync.
        var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM pg_indexes " +
            "WHERE schemaname = 'public' " +
            "  AND tablename  = 'contact_submissions' " +
            "  AND indexname  = 'ix_contact_submissions_received_at';";

        // Act
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        await conn.CloseAsync();

        // Assert
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task MigrateAsync_OnCleanDatabase_PrimaryKeyIndexExists()
    {
        // The PK index must also be present (negative guard: not zero).
        var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM pg_indexes " +
            "WHERE schemaname = 'public' " +
            "  AND tablename  = 'contact_submissions' " +
            "  AND indexname  = 'PK_contact_submissions';";

        var count = (long)(await cmd.ExecuteScalarAsync())!;
        await conn.CloseAsync();

        Assert.True(count >= 1L, "PK index should exist after migration.");
    }

    // =========================================================================
    // AC1 & AC3 — Entity round-trip: insert then retrieve with all fields intact.
    // =========================================================================

    [Fact]
    public async Task Insert_AndRetrieve_RoundTripsAllFields()
    {
        // Arrange
        var expected = BuildSubmission(
            fullName  : "Alice Smith",
            email     : "alice@example.com",
            phone     : "+44-20-7946-0958",
            subject   : "Integration Test Subject",
            message   : "This is the full message body for the round-trip test.",
            receivedAt: new DateTime(2026, 1, 15, 9, 30, 0));

        // Act
        _db.ContactSubmissions.Add(expected);
        await _db.SaveChangesAsync();

        // Clear EF tracking so we definitely hit the database on the next read.
        _db.ChangeTracker.Clear();

        var actual = await _db.ContactSubmissions
            .AsNoTracking()
            .SingleAsync(c => c.Id == expected.Id);

        // Assert — every field must match
        Assert.Equal(expected.Id,        actual.Id);
        Assert.Equal(expected.FullName,  actual.FullName);
        Assert.Equal(expected.Email,     actual.Email);
        Assert.Equal(expected.Phone,     actual.Phone);
        Assert.Equal(expected.Subject,   actual.Subject);
        Assert.Equal(expected.Message,   actual.Message);
        // Compare UTC ticks; Postgres returns timestamptz as Utc.
        Assert.Equal(
            expected.ReceivedAt.ToUniversalTime(),
            actual.ReceivedAt.ToUniversalTime());
    }

    [Fact]
    public async Task Insert_NonExistentId_ReturnsNull()
    {
        // Negative: querying for an Id that was never inserted returns null.
        var missing = Guid.NewGuid();

        var result = await _db.ContactSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == missing);

        Assert.Null(result);
    }

    [Fact]
    public async Task Insert_MultipleSubmissions_ReturnsAllOfThem()
    {
        // Arrange
        var first  = BuildSubmission(fullName: "Bob Jones",   email: "bob@example.com",   phone: "111", subject: "S1", message: "M1");
        var second = BuildSubmission(fullName: "Carol White",  email: "carol@example.com", phone: "222", subject: "S2", message: "M2");

        // Act
        _db.ContactSubmissions.AddRange(first, second);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var ids = new[] { first.Id, second.Id };
        var results = await _db.ContactSubmissions
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToListAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.FullName == "Bob Jones");
        Assert.Contains(results, r => r.FullName == "Carol White");
    }

    // =========================================================================
    // AC1 — Field-by-field column mapping verification via raw SQL read-back.
    // =========================================================================

    [Fact]
    public async Task Insert_VerifiesSnakeCaseColumnNamesViaRawSql()
    {
        // Arrange — insert a known submission
        var submission = BuildSubmission(
            fullName : "Dan Brown",
            email    : "dan@example.com",
            phone    : "333-4444",
            subject  : "Snake case check",
            message  : "Column names must be snake_case.");

        _db.ContactSubmissions.Add(submission);
        await _db.SaveChangesAsync();

        // Act — read back using explicit snake_case column aliases
        var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, full_name, email, phone, subject, message, received_at " +
            "FROM contact_submissions " +
            $"WHERE id = '{submission.Id}';";

        await using var reader = await cmd.ExecuteReaderAsync();
        var found = await reader.ReadAsync();

        // Assert
        Assert.True(found, "Row should be found by explicit snake_case column query.");
        Assert.Equal(submission.Id,       reader.GetGuid(reader.GetOrdinal("id")));
        Assert.Equal(submission.FullName, reader.GetString(reader.GetOrdinal("full_name")));
        Assert.Equal(submission.Email,    reader.GetString(reader.GetOrdinal("email")));
        Assert.Equal(submission.Phone,    reader.GetString(reader.GetOrdinal("phone")));
        Assert.Equal(submission.Subject,  reader.GetString(reader.GetOrdinal("subject")));
        Assert.Equal(submission.Message,  reader.GetString(reader.GetOrdinal("message")));

        await conn.CloseAsync();
    }

    [Fact]
    public async Task Insert_WithMinimalFieldLengths_Succeeds()
    {
        // Boundary: single-character values for every string field.
        var submission = BuildSubmission(
            fullName: "A",
            email:    "a@b.co",
            phone:    "0",
            subject:  "X",
            message:  "Y");

        _db.ContactSubmissions.Add(submission);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var actual = await _db.ContactSubmissions
            .AsNoTracking()
            .SingleAsync(c => c.Id == submission.Id);

        Assert.Equal("A",     actual.FullName);
        Assert.Equal("a@b.co", actual.Email);
        Assert.Equal("0",     actual.Phone);
        Assert.Equal("X",     actual.Subject);
        Assert.Equal("Y",     actual.Message);
    }

    [Fact]
    public async Task Insert_WithMaxFieldLengths_Succeeds()
    {
        // Boundary: values at declared max lengths (200 / 320 / 50 / 200 / 1000).
        var submission = BuildSubmission(
            fullName: new string('F', 200),
            email:    new string('e', 308) + "@x.co",   // 308 + 5 = 313 ≤ 320
            phone:    new string('9', 50),
            subject:  new string('S', 200),
            message:  new string('M', 1000));

        _db.ContactSubmissions.Add(submission);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var actual = await _db.ContactSubmissions
            .AsNoTracking()
            .SingleAsync(c => c.Id == submission.Id);

        Assert.Equal(200,  actual.FullName.Length);
        Assert.Equal(50,   actual.Phone.Length);
        Assert.Equal(200,  actual.Subject.Length);
        Assert.Equal(1000, actual.Message.Length);
    }
}
