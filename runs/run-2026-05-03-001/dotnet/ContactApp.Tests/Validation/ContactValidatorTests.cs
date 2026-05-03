using ContactApp.Api.Models;
using ContactApp.Api.Validation;

namespace ContactApp.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="ContactValidator"/>.
/// Each test exercises a single validation rule in isolation so that boundary
/// conditions (field length limits, email regex edge cases, whitespace trimming)
/// are proved without going through the HTTP layer.
///
/// Coverage map:
///   AC1  – trimming before save (validator trims before evaluating length)
///   AC2  – empty / whitespace-only fullName → errors.fullName
///   AC3  – email regex ^[^\s@]+@[^\s@]+\.[^\s@]+$  (invalid and valid boundaries)
///   AC4  – message length 5001 → errors.message
///   AC5  – multiple invalid fields → all offending keys present simultaneously
/// </summary>
public class ContactValidatorTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static Dictionary<string, string[]> Validate(
        string? fullName = "Jane Doe",
        string? email = "jane@example.com",
        string? message = "Hello world.")
        => ContactValidator.Validate(new ContactSubmissionRequest(fullName, email, message));

    // ──────────────────────────────────────────────────────────────────────────
    // Happy-path: all valid → empty errors dictionary
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_AllFieldsValid_ReturnsEmptyErrors()
    {
        // Arrange / Act
        var errors = Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MessageExactly5000Chars_ReturnsEmptyErrors()
    {
        // Arrange — boundary: 5000 chars is the maximum allowed length.
        var errors = Validate(message: new string('a', 5000));

        // Assert
        Assert.False(errors.ContainsKey("message"),
            "A 5000-character message must NOT produce a message error.");
    }

    [Fact]
    public void Validate_FullNameExactly200Chars_ReturnsEmptyErrors()
    {
        // Arrange — boundary: 200 chars is the maximum allowed length.
        var errors = Validate(fullName: new string('A', 200));

        // Assert
        Assert.False(errors.ContainsKey("fullName"),
            "A 200-character full name must NOT produce a fullName error.");
    }

    [Fact]
    public void Validate_EmailExactly320Chars_ReturnsEmptyErrors()
    {
        // Arrange — boundary: 320 chars is the maximum allowed email length.
        // Build a valid email that is exactly 320 characters:
        // local@host.<domain-padding> where total == 320.
        // "a@b." = 4 chars; we need 316 more 'x' chars for the TLD part.
        var localPart = "a";
        var domain = "b";
        var tld = new string('x', 320 - localPart.Length - 1 - domain.Length - 1); // "@" + "."
        var longEmail = $"{localPart}@{domain}.{tld}";
        Assert.Equal(320, longEmail.Length);

        var errors = Validate(email: longEmail);

        // Assert
        Assert.False(errors.ContainsKey("email"),
            "A 320-character valid email must NOT produce an email error.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AC2 – FullName validation
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_FullName_Empty_AddsFullNameError()
    {
        // Arrange / Act
        var errors = Validate(fullName: "");

        // Assert
        Assert.True(errors.ContainsKey("fullName"),
            "An empty fullName must produce a fullName validation error.");
        Assert.NotEmpty(errors["fullName"]);
    }

    [Fact]
    public void Validate_FullName_Null_AddsFullNameError()
    {
        var errors = Validate(fullName: null);

        Assert.True(errors.ContainsKey("fullName"),
            "A null fullName must produce a fullName validation error.");
    }

    [Fact]
    public void Validate_FullName_WhitespaceOnly_AddsFullNameError()
    {
        // Arrange — validator trims before checking length; whitespace-only
        // trims to an empty string and must be treated as missing (AC2 / AC1).
        var errors = Validate(fullName: "   ");

        Assert.True(errors.ContainsKey("fullName"),
            "A whitespace-only fullName must be treated as empty after trimming.");
    }

    [Fact]
    public void Validate_FullName_201Chars_AddsFullNameError()
    {
        // Arrange — one over the 200-character limit.
        var errors = Validate(fullName: new string('A', 201));

        Assert.True(errors.ContainsKey("fullName"),
            "A 201-character fullName must exceed the max-length rule.");
        Assert.NotEmpty(errors["fullName"]);
    }

    [Fact]
    public void Validate_FullName_TrimmedTo201Chars_DoesNotAddError_WhenTrimmedIsExactly200()
    {
        // Arrange — surround a 200-char name with leading/trailing spaces.
        // After trimming it is exactly 200 chars → valid.
        var paddedName = "  " + new string('B', 200) + "  ";
        var errors = Validate(fullName: paddedName);

        Assert.False(errors.ContainsKey("fullName"),
            "A fullName that trims to exactly 200 chars must be valid.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AC3 – Email validation (regex ^[^\s@]+@[^\s@]+\.[^\s@]+$)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_Email_Empty_AddsEmailError()
    {
        var errors = Validate(email: "");

        Assert.True(errors.ContainsKey("email"),
            "An empty email must produce an email validation error.");
    }

    [Fact]
    public void Validate_Email_Null_AddsEmailError()
    {
        var errors = Validate(email: null);

        Assert.True(errors.ContainsKey("email"),
            "A null email must produce an email validation error.");
    }

    [Fact]
    public void Validate_Email_NotAnEmail_AddsEmailError()
    {
        // AC3 canonical example.
        var errors = Validate(email: "not-an-email");

        Assert.True(errors.ContainsKey("email"),
            "'not-an-email' must fail the email regex.");
    }

    [Fact]
    public void Validate_Email_MissingAtSign_AddsEmailError()
    {
        // No '@' character → regex must reject.
        var errors = Validate(email: "nodomain.com");

        Assert.True(errors.ContainsKey("email"),
            "An address without '@' must fail the email regex.");
    }

    [Fact]
    public void Validate_Email_MissingDot_AddsEmailError()
    {
        // Has '@' but domain has no dot → regex must reject.
        var errors = Validate(email: "user@nodot");

        Assert.True(errors.ContainsKey("email"),
            "An address without a dot in the domain must fail the email regex.");
    }

    [Fact]
    public void Validate_Email_WithSpaceInLocalPart_AddsEmailError()
    {
        // Regex [^\s@] forbids whitespace in any position.
        var errors = Validate(email: "user name@example.com");

        Assert.True(errors.ContainsKey("email"),
            "An email with a space in the local part must fail the email regex.");
    }

    [Fact]
    public void Validate_Email_WithSpaceInDomain_AddsEmailError()
    {
        var errors = Validate(email: "user@exam ple.com");

        Assert.True(errors.ContainsKey("email"),
            "An email with a space in the domain must fail the email regex.");
    }

    [Fact]
    public void Validate_Email_AtSignAtStart_AddsEmailError()
    {
        // Local part is empty → [^\s@]+ requires at least one character.
        var errors = Validate(email: "@example.com");

        Assert.True(errors.ContainsKey("email"),
            "An address starting with '@' (empty local part) must fail the email regex.");
    }

    [Fact]
    public void Validate_Email_AtSignAtEnd_AddsEmailError()
    {
        // Domain part is empty after '@' → must fail.
        var errors = Validate(email: "user@");

        Assert.True(errors.ContainsKey("email"),
            "An address ending with '@' (empty domain) must fail the email regex.");
    }

    [Fact]
    public void Validate_Email_Valid_DoesNotAddEmailError()
    {
        // Happy-path boundary: minimal valid address.
        var errors = Validate(email: "a@b.c");

        Assert.False(errors.ContainsKey("email"),
            "'a@b.c' is a valid email and must not produce an error.");
    }

    [Fact]
    public void Validate_Email_ValidWithSubdomain_DoesNotAddEmailError()
    {
        var errors = Validate(email: "user@mail.example.com");

        Assert.False(errors.ContainsKey("email"),
            "A valid email with a subdomain must not produce an error.");
    }

    [Fact]
    public void Validate_Email_ValidWithPlusAlias_DoesNotAddEmailError()
    {
        // '+' is not whitespace and not '@', so regex accepts it.
        var errors = Validate(email: "user+tag@example.com");

        Assert.False(errors.ContainsKey("email"),
            "An email with a plus-alias must not produce an error.");
    }

    [Fact]
    public void Validate_Email_WhitespacePadded_IsValidAfterTrim()
    {
        // The validator trims before evaluating; padded valid email must pass.
        var errors = Validate(email: "  user@example.com  ");

        Assert.False(errors.ContainsKey("email"),
            "An email that is valid after trimming must not produce an error.");
    }

    [Fact]
    public void Validate_Email_321Chars_AddsEmailError()
    {
        // One character over the 320-char email max.
        var localPart = "a";
        var domain = "b";
        var tld = new string('x', 321 - localPart.Length - 1 - domain.Length - 1);
        var tooLongEmail = $"{localPart}@{domain}.{tld}";
        Assert.Equal(321, tooLongEmail.Length);

        var errors = Validate(email: tooLongEmail);

        Assert.True(errors.ContainsKey("email"),
            "A 321-character email must exceed the max-length rule.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AC4 – Message validation
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_Message_Empty_AddsMessageError()
    {
        var errors = Validate(message: "");

        Assert.True(errors.ContainsKey("message"),
            "An empty message must produce a message validation error.");
    }

    [Fact]
    public void Validate_Message_Null_AddsMessageError()
    {
        var errors = Validate(message: null);

        Assert.True(errors.ContainsKey("message"),
            "A null message must produce a message validation error.");
    }

    [Fact]
    public void Validate_Message_WhitespaceOnly_AddsMessageError()
    {
        // Validator trims before checking; whitespace-only trims to empty.
        var errors = Validate(message: "     ");

        Assert.True(errors.ContainsKey("message"),
            "A whitespace-only message must be treated as empty after trimming.");
    }

    [Fact]
    public void Validate_Message_5001Chars_AddsMessageError()
    {
        // AC4 canonical example: exactly one character over the 5000-char limit.
        var errors = Validate(message: new string('x', 5001));

        Assert.True(errors.ContainsKey("message"),
            "A 5001-character message must exceed the max-length rule.");
        Assert.NotEmpty(errors["message"]);
    }

    [Fact]
    public void Validate_Message_TrimmedTo5000Chars_DoesNotAddError()
    {
        // A message that trims to exactly 5000 chars must be valid.
        var paddedMessage = " " + new string('m', 5000) + " ";
        var errors = Validate(message: paddedMessage);

        Assert.False(errors.ContainsKey("message"),
            "A message that trims to exactly 5000 chars must be valid.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AC5 – Multiple invalid fields all appear in the errors dictionary
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_AllFieldsInvalid_ReturnsAllThreeErrors()
    {
        // Arrange — each field violates a different rule simultaneously.
        var errors = Validate(
            fullName: "",
            email: "not-an-email",
            message: "");

        // Assert — all three keys must be present in a single pass.
        Assert.True(errors.ContainsKey("fullName"),
            "errors must contain 'fullName' when fullName is empty.");
        Assert.True(errors.ContainsKey("email"),
            "errors must contain 'email' when email is invalid.");
        Assert.True(errors.ContainsKey("message"),
            "errors must contain 'message' when message is empty.");
        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void Validate_TwoFieldsInvalid_ReturnsBothErrors()
    {
        // fullName valid, email and message both invalid.
        var errors = Validate(
            fullName: "Alice",
            email: "not-an-email",
            message: "");

        Assert.True(errors.ContainsKey("email"));
        Assert.True(errors.ContainsKey("message"));
        Assert.False(errors.ContainsKey("fullName"),
            "errors must NOT contain 'fullName' when fullName is valid.");
        Assert.Equal(2, errors.Count);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AC1 – Trimming: validator trims values so storage receives clean strings
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_FullName_WithLeadingAndTrailingSpaces_IsValid()
    {
        // A name that has surrounding whitespace but is non-empty after trim
        // must pass validation (the actual stored value will be trimmed by the
        // endpoint before persisting — the validator mirrors that behaviour).
        var errors = Validate(fullName: "  John Smith  ");

        Assert.False(errors.ContainsKey("fullName"),
            "A fullName with only surrounding whitespace must be valid after trim.");
    }

    [Fact]
    public void Validate_Message_WithSurroundingWhitespace_IsValid()
    {
        var errors = Validate(message: "  Some message content  ");

        Assert.False(errors.ContainsKey("message"),
            "A message with only surrounding whitespace must be valid after trim.");
    }
}
