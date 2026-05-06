using System.Text.Json.Serialization;

namespace ContactApp.Api.Dtos;

/// <summary>
/// Request DTO for submitting a contact form entry.
/// Properties are PascalCase; JSON serialization uses camelCase via the global
/// JsonSerializerOptions (or the JsonPropertyName attributes below as a fallback).
/// </summary>
public sealed class ContactSubmissionRequest
{
    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
