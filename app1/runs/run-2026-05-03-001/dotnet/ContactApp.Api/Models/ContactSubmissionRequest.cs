namespace ContactApp.Api.Models;

/// <summary>
/// Request body for POST /api/contact.
/// </summary>
/// <param name="FullName">Submitter's full name.</param>
/// <param name="Email">Submitter's email address.</param>
/// <param name="Message">Message body.</param>
public record ContactSubmissionRequest(
    string? FullName,
    string? Email,
    string? Message);
