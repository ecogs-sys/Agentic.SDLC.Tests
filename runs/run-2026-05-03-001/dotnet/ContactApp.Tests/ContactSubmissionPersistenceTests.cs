using ContactApp.Api.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ContactApp.Tests;

/// <summary>
/// Integration tests for the contact submission persistence layer (TECH-005).
/// Each test starts a real PostgreSQL instance via Testcontainers, applies EF Core
/// migrations, exercises the append-only insert path, and tears down the container.
/// </summary>
public sealed class ContactSubmissionPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    private ContactDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ContactDbContext(options);
    }

    /// <summary>
    /// AC4: Inserting a ContactSubmission via DbContext and reading it back returns
    /// a row whose every column value matches what was written.
    /// </summary>
    [Fact]
    public async Task Insert_AndReadBack_ReturnsExpectedValues()
    {
        // Arrange — apply migrations against the fresh container database.
        await using var setupCtx = CreateContext();
        await setupCtx.Database.MigrateAsync();

        var expectedId = Guid.NewGuid();
        var expectedReceivedAt = new DateTime(2026, 5, 3, 12, 0, 0, DateTimeKind.Utc);

        var submission = new ContactSubmission
        {
            Id = expectedId,
            FullName = "Jane Doe",
            Email = "jane.doe@example.com",
            Message = "Hello, I would like to get in touch.",
            ReceivedAt = expectedReceivedAt,
        };

        // Act — insert via the write context, read back via a separate context
        // (ensures no first-level cache gives a false positive).
        await using (var writeCtx = CreateContext())
        {
            writeCtx.Submissions.Add(submission);
            await writeCtx.SaveChangesAsync();
        }

        ContactSubmission? readBack;
        await using (var readCtx = CreateContext())
        {
            readBack = await readCtx.Submissions.FindAsync(expectedId);
        }

        // Assert
        Assert.NotNull(readBack);
        Assert.Equal(expectedId, readBack.Id);
        Assert.Equal("Jane Doe", readBack.FullName);
        Assert.Equal("jane.doe@example.com", readBack.Email);
        Assert.Equal("Hello, I would like to get in touch.", readBack.Message);
        Assert.Equal(expectedReceivedAt, readBack.ReceivedAt.ToUniversalTime());
    }

    /// <summary>
    /// AC5: The DbContext exposes no Remove or Update code paths.
    /// The Submissions DbSet is declared but no helper methods wrapping
    /// Remove/Update exist on ContactDbContext. This test verifies the schema
    /// constraint: a second insert with a conflicting PK throws, confirming
    /// the store is append-only (no upsert/update silently succeeds).
    /// </summary>
    [Fact]
    public async Task DuplicateInsert_ThrowsException_ConfirmingNoPkUpsert()
    {
        await using var setupCtx = CreateContext();
        await setupCtx.Database.MigrateAsync();

        var id = Guid.NewGuid();

        await using (var ctx1 = CreateContext())
        {
            ctx1.Submissions.Add(new ContactSubmission
            {
                Id = id,
                FullName = "Alice",
                Email = "alice@example.com",
                Message = "First message.",
                ReceivedAt = DateTime.UtcNow,
            });
            await ctx1.SaveChangesAsync();
        }

        // Inserting a second row with the same PK must fail — no silent update.
        await using var ctx2 = CreateContext();
        ctx2.Submissions.Add(new ContactSubmission
        {
            Id = id,  // same PK — must violate the PK constraint
            FullName = "Alice Modified",
            Email = "alice@example.com",
            Message = "Attempted overwrite.",
            ReceivedAt = DateTime.UtcNow,
        });

        await Assert.ThrowsAnyAsync<Exception>(() => ctx2.SaveChangesAsync());
    }
}
