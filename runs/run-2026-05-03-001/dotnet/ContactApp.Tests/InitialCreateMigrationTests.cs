using System.Reflection;
using ContactApp.Api.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ContactApp.Tests;

/// <summary>
/// AC3 – Unit tests that inspect the InitialCreate migration's Up() method directly
/// (without a database) to verify the table name, every required column (name + type),
/// and the index name are exactly as specified.
/// </summary>
public class InitialCreateMigrationTests
{
    /// <summary>
    /// Captures the operations produced by Up() by subclassing MigrationBuilder.
    /// </summary>
    private static IReadOnlyList<MigrationOperation> CollectUpOperations()
    {
        // MigrationBuilder accumulates operations in its Operations list.
        var builder = new MigrationBuilder(activeProvider: "Npgsql");
        var migration = new InitialCreate();

        // Up() is protected — invoke via reflection.
        var upMethod = typeof(InitialCreate)
            .GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance)!;
        upMethod.Invoke(migration, new object[] { builder });

        return builder.Operations;
    }

    private static CreateTableOperation GetCreateTableOp()
    {
        var ops = CollectUpOperations();
        var createOp = ops.OfType<CreateTableOperation>().SingleOrDefault();
        Assert.NotNull(createOp);
        return createOp!;
    }

    // ---- Table name --------------------------------------------------------

    [Fact]
    public void Up_CreatesTable_Named_contact_submissions()
    {
        // Arrange / Act
        var op = GetCreateTableOp();

        // Assert
        Assert.Equal("contact_submissions", op.Name);
    }

    // ---- Columns -----------------------------------------------------------

    [Theory]
    [InlineData("id",          "uuid")]
    [InlineData("full_name",   "varchar(200)")]
    [InlineData("email",       "varchar(320)")]
    [InlineData("message",     "text")]
    [InlineData("received_at", "timestamptz")]
    public void Up_CreatesTable_WithColumn_CorrectNameAndType(string columnName, string columnType)
    {
        // Arrange
        var op = GetCreateTableOp();

        // Act
        var col = op.Columns.SingleOrDefault(c => c.Name == columnName);

        // Assert
        Assert.NotNull(col);
        Assert.Equal(columnType, col!.ColumnType);
    }

    [Fact]
    public void Up_CreatesTable_WithNonNullable_full_name()
    {
        var op = GetCreateTableOp();
        var col = op.Columns.Single(c => c.Name == "full_name");
        Assert.False(col.IsNullable);
    }

    [Fact]
    public void Up_CreatesTable_WithNonNullable_email()
    {
        var op = GetCreateTableOp();
        var col = op.Columns.Single(c => c.Name == "email");
        Assert.False(col.IsNullable);
    }

    [Fact]
    public void Up_CreatesTable_WithNonNullable_message()
    {
        var op = GetCreateTableOp();
        var col = op.Columns.Single(c => c.Name == "message");
        Assert.False(col.IsNullable);
    }

    [Fact]
    public void Up_CreatesTable_WithNonNullable_received_at()
    {
        var op = GetCreateTableOp();
        var col = op.Columns.Single(c => c.Name == "received_at");
        Assert.False(col.IsNullable);
    }

    // ---- Primary key -------------------------------------------------------

    [Fact]
    public void Up_CreatesTable_WithPrimaryKey_On_id()
    {
        var op = GetCreateTableOp();
        Assert.NotNull(op.PrimaryKey);
        Assert.Contains("id", op.PrimaryKey!.Columns);
    }

    // ---- Index -------------------------------------------------------------

    [Fact]
    public void Up_CreatesIndex_Named_ix_contact_submissions_received_at()
    {
        // Arrange / Act
        var ops = CollectUpOperations();
        var indexOp = ops.OfType<CreateIndexOperation>().SingleOrDefault();

        // Assert
        Assert.NotNull(indexOp);
        Assert.Equal("ix_contact_submissions_received_at", indexOp!.Name);
        Assert.Equal("contact_submissions", indexOp.Table);
        Assert.Contains("received_at", indexOp.Columns);
    }

    // ---- Down does not leave orphaned operations above Up's table ----------

    [Fact]
    public void Down_DropsTable_Named_contact_submissions()
    {
        var builder = new MigrationBuilder(activeProvider: "Npgsql");
        var migration = new InitialCreate();

        var downMethod = typeof(InitialCreate)
            .GetMethod("Down", BindingFlags.NonPublic | BindingFlags.Instance)!;
        downMethod.Invoke(migration, new object[] { builder });

        var dropOp = builder.Operations.OfType<DropTableOperation>().SingleOrDefault();
        Assert.NotNull(dropOp);
        Assert.Equal("contact_submissions", dropOp!.Name);
    }
}
