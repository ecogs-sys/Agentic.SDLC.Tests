using ContactApp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ContactApp.Api.Data;

/// <summary>
/// EF Core DbContext for the ContactApp. Uses the Npgsql provider with
/// snake_case naming so the table is <c>contact_submissions</c> and all
/// columns follow the snake_case convention (e.g. <c>full_name</c>, <c>received_at</c>).
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ContactSubmission>(entity =>
        {
            entity.ToTable("contact_submissions");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.FullName)
                  .HasColumnName("full_name")
                  .HasColumnType("varchar(200)")
                  .IsRequired();

            entity.Property(e => e.Email)
                  .HasColumnName("email")
                  .HasColumnType("varchar(320)")
                  .IsRequired();

            entity.Property(e => e.Phone)
                  .HasColumnName("phone")
                  .HasColumnType("varchar(50)")
                  .IsRequired();

            entity.Property(e => e.Subject)
                  .HasColumnName("subject")
                  .HasColumnType("varchar(200)")
                  .IsRequired();

            entity.Property(e => e.Message)
                  .HasColumnName("message")
                  .HasColumnType("varchar(1000)")
                  .IsRequired();

            entity.Property(e => e.ReceivedAt)
                  .HasColumnName("received_at")
                  .HasColumnType("timestamp with time zone")
                  .IsRequired()
                  .HasDefaultValueSql("now() at time zone 'utc'");

            entity.HasIndex(e => e.ReceivedAt)
                  .HasDatabaseName("ix_contact_submissions_received_at");
        });
    }
}
