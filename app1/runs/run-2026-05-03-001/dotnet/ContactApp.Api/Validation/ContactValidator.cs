using System.Text.RegularExpressions;
using ContactApp.Api.Models;

namespace ContactApp.Api.Validation;

/// <summary>
/// Server-side validation for <see cref="ContactSubmissionRequest"/>.
/// Mirrors the client-side rules defined in TECH-002.
/// </summary>
public static class ContactValidator
{
    // Regex from TECH-004 / TECH-002.
    private static readonly Regex EmailRegex =
        new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));

    /// <summary>
    /// Validates the request and returns a dictionary of field errors.
    /// Keys use camelCase to match the JSON property naming convention.
    /// Returns an empty dictionary when the request is valid.
    /// </summary>
    public static Dictionary<string, string[]> Validate(ContactSubmissionRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateFullName(request.FullName, errors);
        ValidateEmail(request.Email, errors);
        ValidateMessage(request.Message, errors);

        return errors;
    }

    private static void ValidateFullName(string? value, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            errors["fullName"] = ["Full name is required."];
            return;
        }

        if (trimmed.Length > 200)
        {
            errors["fullName"] = ["Full name must not exceed 200 characters."];
        }
    }

    private static void ValidateEmail(string? value, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            errors["email"] = ["Email address is required."];
            return;
        }

        if (trimmed.Length > 320)
        {
            errors["email"] = ["Email address must not exceed 320 characters."];
            return;
        }

        if (!EmailRegex.IsMatch(trimmed))
        {
            errors["email"] = ["Please enter a valid email address."];
        }
    }

    private static void ValidateMessage(string? value, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            errors["message"] = ["Message cannot be empty."];
            return;
        }

        if (trimmed.Length > 5000)
        {
            errors["message"] = ["Message must not exceed 5000 characters."];
        }
    }
}
