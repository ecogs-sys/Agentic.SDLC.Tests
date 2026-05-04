using Microsoft.EntityFrameworkCore;

namespace ContactApp.Api.Data;

/// <summary>
/// EF Core DbContext for the contact submission persistence layer.
/// Exposes only <see cref="Submissions"/>; no Update or Remove helpers are provided
/// because this store is append-only.
/// </summary>
public class ContactDbContext : DbContext
{
    public ContactDbContext(DbContextOptions<ContactDbContext> options)
        : base(options)
    {
    }

    /// <summary>Append-only set of contact submissions.</summary>
    public DbSet<ContactSubmission> Submissions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ContactSubmission>(entity =>
        {
            entity.ToTable("contact_submissions");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .HasColumnName("id")
                  .HasColumnType("uuid")
                  .ValueGeneratedNever(); // server assigns Guid.NewGuid() before Add()

            entity.Property(e => e.FullName)
                  .HasColumnName("full_name")
                  .HasColumnType("varchar(200)")
                  .IsRequired();

            entity.Property(e => e.Email)
                  .HasColumnName("email")
                  .HasColumnType("varchar(320)")
                  .IsRequired();

            entity.Property(e => e.Message)
                  .HasColumnName("message")
                  .HasColumnType("text")
                  .IsRequired();

            entity.Property(e => e.ReceivedAt)
                  .HasColumnName("received_at")
                  .HasColumnType("timestamptz")
                  .IsRequired();

            entity.HasIndex(e => e.ReceivedAt)
                  .HasDatabaseName("ix_contact_submissions_received_at");
        });
    }
}
