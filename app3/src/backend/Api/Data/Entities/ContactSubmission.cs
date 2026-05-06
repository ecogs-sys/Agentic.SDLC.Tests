namespace ContactApp.Api.Data.Entities;

/// <summary>
/// Represents a single contact form submission persisted to the database.
/// Each row is append-only; no updates are ever applied by application code.
/// </summary>
public sealed class ContactSubmission
{
    /// <summary>Primary key. Set to <see cref="Guid.NewGuid"/> by the application before insert.</summary>
    public Guid Id { get; set; }

    /// <summary>Submitter's full name (max 200 characters).</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Submitter's email address (max 320 characters).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Submitter's phone number (max 50 characters).</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Subject of the enquiry (max 200 characters).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Body of the enquiry (max 1000 characters).</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the submission was received.</summary>
    public DateTime ReceivedAt { get; set; }
}
