namespace ContactApp.Api.Data;

/// <summary>
/// Represents a contact enquiry submitted by a site visitor.
/// Rows are append-only; no update or delete code paths exist.
/// </summary>
public class ContactSubmission
{
    /// <summary>Server-assigned primary key (UUID v4).</summary>
    public Guid Id { get; set; }

    /// <summary>Submitter's full name. Max 200 characters.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Submitter's email address. Max 320 characters.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Message body. Stored as text (unbounded in the column type).</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>UTC timestamp assigned by the server at insertion time.</summary>
    public DateTime ReceivedAt { get; set; }
}
