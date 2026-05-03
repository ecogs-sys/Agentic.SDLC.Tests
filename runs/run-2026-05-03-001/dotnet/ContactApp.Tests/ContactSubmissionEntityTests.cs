using ContactApp.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ContactApp.Tests;

/// <summary>
/// Unit tests for AC1 and AC2:
///   AC1 – ContactSubmission entity has the required properties with the correct CLR types.
///   AC2 – ContactDbContext exposes DbSet&lt;ContactSubmission&gt; Submissions and the column
///          mappings declared in OnModelCreating match the specification.
///
/// The AC2 model tests build the EF Core model via UseNpgsql with a dummy connection string
/// so that relational metadata (column names, column types) is fully populated without
/// requiring a live database connection.
/// </summary>
public class ContactSubmissionEntityTests
{
    // -------------------------------------------------------------------------
    // AC1 – Entity property types
    // -------------------------------------------------------------------------

    [Fact]
    public void Entity_Id_PropertyType_IsGuid()
    {
        // Arrange / Act
        var prop = typeof(ContactSubmission).GetProperty(nameof(ContactSubmission.Id));

        // Assert
        Assert.NotNull(prop);
        Assert.Equal(typeof(Guid), prop!.PropertyType);
    }

    [Fact]
    public void Entity_FullName_PropertyType_IsString()
    {
        var prop = typeof(ContactSubmission).GetProperty(nameof(ContactSubmission.FullName));

        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void Entity_Email_PropertyType_IsString()
    {
        var prop = typeof(ContactSubmission).GetProperty(nameof(ContactSubmission.Email));

        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void Entity_Message_PropertyType_IsString()
    {
        var prop = typeof(ContactSubmission).GetProperty(nameof(ContactSubmission.Message));

        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void Entity_ReceivedAt_PropertyType_IsDateTime()
    {
        var prop = typeof(ContactSubmission).GetProperty(nameof(ContactSubmission.ReceivedAt));

        Assert.NotNull(prop);
        Assert.Equal(typeof(DateTime), prop!.PropertyType);
    }

    [Fact]
    public void Entity_HasExactlyFiveProperties()
    {
        // Arrange
        var expected = new[] { "Id", "FullName", "Email", "Message", "ReceivedAt" };

        // Act
        var actual = typeof(ContactSubmission)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        // Assert – every expected property is present (no extras or missing)
        Assert.Equal(
            expected.OrderBy(n => n).ToArray(),
            actual);
    }

    // -------------------------------------------------------------------------
    // AC2 – DbContext column configuration
    //
    // We use UseNpgsql with a dummy DSN so that EF Core builds the relational
    // model (column names, column types, index database names) without opening
    // any real connection.
    // -------------------------------------------------------------------------

    private static ContactDbContext BuildRelationalModelContext()
    {
        var options = new DbContextOptionsBuilder<ContactDbContext>()
            .UseNpgsql("Host=localhost;Database=test_model_only;Username=u;Password=p")
            .Options;
        return new ContactDbContext(options);
    }

    /// <summary>
    /// Helper that reads the configured column type annotation directly from the
    /// IReadOnlyProperty metadata, bypassing the relational type-mapping resolution
    /// that requires a live provider store.
    /// </summary>
    private static string? GetConfiguredColumnType(IReadOnlyProperty prop)
        => prop.GetAnnotation("Relational:ColumnType").Value as string;

    [Fact]
    public void DbContext_Submissions_DbSet_IsExposed()
    {
        // Arrange / Act – purely reflection-based, no context needed
        var prop = typeof(ContactDbContext).GetProperty(nameof(ContactDbContext.Submissions));

        // Assert
        Assert.NotNull(prop);
        Assert.Equal(typeof(DbSet<ContactSubmission>), prop!.PropertyType);
    }

    [Fact]
    public void DbContext_TableName_Is_contact_submissions()
    {
        // Arrange
        using var ctx = BuildRelationalModelContext();
        var entityType = ctx.Model.FindEntityType(typeof(ContactSubmission))!;

        // Act
        var tableName = entityType.GetTableName();

        // Assert
        Assert.Equal("contact_submissions", tableName);
    }

    [Fact]
    public void DbContext_Column_Id_HasName_id_And_Type_uuid()
    {
        using var ctx = BuildRelationalModelContext();
        var entityType = ctx.Model.FindEntityType(typeof(ContactSubmission))!;
        var prop = entityType.FindProperty(nameof(ContactSubmission.Id))!;

        Assert.Equal("id", prop.GetColumnName());
        Assert.Equal("uuid", GetConfiguredColumnType(prop));
    }

    [Fact]
    public void DbContext_Column_FullName_HasName_full_name_And_Type_varchar200()
    {
        using var ctx = BuildRelationalModelContext();
        var entityType = ctx.Model.FindEntityType(typeof(ContactSubmission))!;
        var prop = entityType.FindProperty(nameof(ContactSubmission.FullName))!;

        Assert.Equal("full_name", prop.GetColumnName());
        Assert.Equal("varchar(200)", GetConfiguredColumnType(prop));
        Assert.False(prop.IsNullable);
    }

    [Fact]
    public void DbContext_Column_Email_HasName_email_And_Type_varchar320()
    {
        using var ctx = BuildRelationalModelContext();
        var entityType = ctx.Model.FindEntityType(typeof(ContactSubmission))!;
        var prop = entityType.FindProperty(nameof(ContactSubmission.Email))!;

        Assert.Equal("email", prop.GetColumnName());
        Assert.Equal("varchar(320)", GetConfiguredColumnType(prop));
        Assert.False(prop.IsNullable);
    }

    [Fact]
    public void DbContext_Column_Message_HasName_message_And_Type_text()
    {
        using var ctx = BuildRelationalModelContext();
        var entityType = ctx.Model.FindEntityType(typeof(ContactSubmission))!;
        var prop = entityType.FindProperty(nameof(ContactSubmission.Message))!;

        Assert.Equal("message", prop.GetColumnName());
        Assert.Equal("text", GetConfiguredColumnType(prop));
        Assert.False(prop.IsNullable);
    }

    [Fact]
    public void DbContext_Column_ReceivedAt_HasName_received_at_And_Type_timestamptz()
    {
        using var ctx = BuildRelationalModelContext();
        var entityType = ctx.Model.FindEntityType(typeof(ContactSubmission))!;
        var prop = entityType.FindProperty(nameof(ContactSubmission.ReceivedAt))!;

        Assert.Equal("received_at", prop.GetColumnName());
        Assert.Equal("timestamptz", GetConfiguredColumnType(prop));
        Assert.False(prop.IsNullable);
    }

    [Fact]
    public void DbContext_Index_ReceivedAt_HasCorrectDatabaseName()
    {
        using var ctx = BuildRelationalModelContext();
        var entityType = ctx.Model.FindEntityType(typeof(ContactSubmission))!;

        var index = entityType
            .GetIndexes()
            .SingleOrDefault(i =>
                i.Properties.Count == 1 &&
                i.Properties[0].Name == nameof(ContactSubmission.ReceivedAt));

        Assert.NotNull(index);
        Assert.Equal("ix_contact_submissions_received_at", index!.GetDatabaseName());
    }
}
