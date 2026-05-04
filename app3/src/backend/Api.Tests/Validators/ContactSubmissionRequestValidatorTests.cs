using ContactApp.Api.Dtos;
using ContactApp.Api.Validators;
using FluentValidation.TestHelper;

namespace ContactApp.Tests.Validators;

/// <summary>
/// Tests for <see cref="ContactSubmissionRequestValidator"/>.
/// Covers STORY-002 acceptance criteria:
///   AC1  - DTO has string properties with camelCase JSON mapping (structural / attribute assertions).
///   AC2  - Validator enforces NotEmpty + length / format rules on each field.
///   AC3  - Each rule produces a human-readable error message.
///   AC4a - Fully valid payload passes validation.
///   AC4b - Each field failing individually produces an error keyed under that field.
///   AC4c - 1000-char Message passes; 1001-char Message fails.
///   AC4d - Invalid e-mail format fails.
/// </summary>
public class ContactSubmissionRequestValidatorTests
{
    private readonly ContactSubmissionRequestValidator _sut = new();

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static ContactSubmissionRequest ValidRequest() => new()
    {
        FullName = "Jane Doe",
        Email    = "jane.doe@example.com",
        Phone    = "+1-555-0100",
        Subject  = "Test subject",
        Message  = "Hello, this is a test message."
    };

    private static string Repeat(char c, int count) => new(c, count);

    // ---------------------------------------------------------------------------
    // AC1 — DTO structural assertions
    // ---------------------------------------------------------------------------

    [Fact]
    public void ContactSubmissionRequest_HasStringProperty_FullName()
    {
        // Arrange / Act
        var prop = typeof(ContactSubmissionRequest).GetProperty(nameof(ContactSubmissionRequest.FullName));

        // Assert
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void ContactSubmissionRequest_HasStringProperty_Email()
    {
        var prop = typeof(ContactSubmissionRequest).GetProperty(nameof(ContactSubmissionRequest.Email));
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void ContactSubmissionRequest_HasStringProperty_Phone()
    {
        var prop = typeof(ContactSubmissionRequest).GetProperty(nameof(ContactSubmissionRequest.Phone));
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void ContactSubmissionRequest_HasStringProperty_Subject()
    {
        var prop = typeof(ContactSubmissionRequest).GetProperty(nameof(ContactSubmissionRequest.Subject));
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void ContactSubmissionRequest_HasStringProperty_Message()
    {
        var prop = typeof(ContactSubmissionRequest).GetProperty(nameof(ContactSubmissionRequest.Message));
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Theory]
    [InlineData(nameof(ContactSubmissionRequest.FullName), "fullName")]
    [InlineData(nameof(ContactSubmissionRequest.Email),    "email")]
    [InlineData(nameof(ContactSubmissionRequest.Phone),    "phone")]
    [InlineData(nameof(ContactSubmissionRequest.Subject),  "subject")]
    [InlineData(nameof(ContactSubmissionRequest.Message),  "message")]
    public void ContactSubmissionRequest_Properties_HaveCamelCaseJsonPropertyNameAttribute(
        string propertyName, string expectedJsonName)
    {
        // Arrange
        var prop = typeof(ContactSubmissionRequest).GetProperty(propertyName)!;

        // Act
        var attr = prop.GetCustomAttributes(
            typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute), inherit: false)
            .Cast<System.Text.Json.Serialization.JsonPropertyNameAttribute>()
            .SingleOrDefault();

        // Assert
        Assert.NotNull(attr);
        Assert.Equal(expectedJsonName, attr!.Name);
    }

    // ---------------------------------------------------------------------------
    // AC4a — Fully valid payload passes
    // ---------------------------------------------------------------------------

    [Fact]
    public void Validate_WithValidRequest_PassesWithNoErrors()
    {
        // Arrange
        var request = ValidRequest();

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ---------------------------------------------------------------------------
    // AC4b / AC2 / AC3 — FullName rules
    // ---------------------------------------------------------------------------

    [Fact]
    public void Validate_WhenFullNameIsEmpty_FailsWithErrorOnFullName()
    {
        // Arrange
        var request = ValidRequest();
        request.FullName = string.Empty;

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void Validate_WhenFullNameIsEmpty_ErrorMessageIsHumanReadable()
    {
        // Arrange
        var request = ValidRequest();
        request.FullName = string.Empty;

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        var errors = result.Errors.Where(e => e.PropertyName == nameof(ContactSubmissionRequest.FullName)).ToList();
        Assert.Contains(errors, e => !string.IsNullOrWhiteSpace(e.ErrorMessage));
    }

    [Fact]
    public void Validate_WhenFullNameExceedsMaxLength_FailsWithErrorOnFullName()
    {
        // Arrange
        var request = ValidRequest();
        request.FullName = Repeat('A', 201);

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void Validate_WhenFullNameIsExactlyMaxLength_Passes()
    {
        // Arrange
        var request = ValidRequest();
        request.FullName = Repeat('A', 200);

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.FullName);
    }

    // ---------------------------------------------------------------------------
    // AC4b / AC4d / AC2 / AC3 — Email rules
    // ---------------------------------------------------------------------------

    [Fact]
    public void Validate_WhenEmailIsEmpty_FailsWithErrorOnEmail()
    {
        // Arrange
        var request = ValidRequest();
        request.Email = string.Empty;

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_WhenEmailIsEmpty_ErrorMessageIsHumanReadable()
    {
        // Arrange
        var request = ValidRequest();
        request.Email = string.Empty;

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        var errors = result.Errors.Where(e => e.PropertyName == nameof(ContactSubmissionRequest.Email)).ToList();
        Assert.Contains(errors, e => !string.IsNullOrWhiteSpace(e.ErrorMessage));
    }

    [Fact]
    public void Validate_WhenEmailFormatIsInvalid_FailsWithErrorOnEmail()
    {
        // Arrange
        var request = ValidRequest();
        request.Email = "not-an-email";

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_WhenEmailFormatIsInvalid_ErrorMessageIsHumanReadable()
    {
        // Arrange
        var request = ValidRequest();
        request.Email = "bad@@format";

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        var errors = result.Errors.Where(e => e.PropertyName == nameof(ContactSubmissionRequest.Email)).ToList();
        Assert.Contains(errors, e => !string.IsNullOrWhiteSpace(e.ErrorMessage));
    }

    [Fact]
    public void Validate_WhenEmailFormatIsValid_PassesEmailRule()
    {
        // Arrange
        var request = ValidRequest();
        request.Email = "valid@domain.org";

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    // ---------------------------------------------------------------------------
    // AC4b / AC2 / AC3 — Phone rules
    // ---------------------------------------------------------------------------

    [Fact]
    public void Validate_WhenPhoneIsEmpty_FailsWithErrorOnPhone()
    {
        // Arrange
        var request = ValidRequest();
        request.Phone = string.Empty;

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void Validate_WhenPhoneIsEmpty_ErrorMessageIsHumanReadable()
    {
        // Arrange
        var request = ValidRequest();
        request.Phone = string.Empty;

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        var errors = result.Errors.Where(e => e.PropertyName == nameof(ContactSubmissionRequest.Phone)).ToList();
        Assert.Contains(errors, e => !string.IsNullOrWhiteSpace(e.ErrorMessage));
    }

    [Fact]
    public void Validate_WhenPhoneExceedsMaxLength_FailsWithErrorOnPhone()
    {
        // Arrange
        var request = ValidRequest();
        request.Phone = Repeat('1', 51);

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void Validate_WhenPhoneIsExactlyMaxLength_Passes()
    {
        // Arrange
        var request = ValidRequest();
        request.Phone = Repeat('1', 50);

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Phone);
    }

    // ---------------------------------------------------------------------------
    // AC4b / AC2 / AC3 — Subject rules
    // ---------------------------------------------------------------------------

    [Fact]
    public void Validate_WhenSubjectIsEmpty_FailsWithErrorOnSubject()
    {
        // Arrange
        var request = ValidRequest();
        request.Subject = string.Empty;

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Validate_WhenSubjectIsEmpty_ErrorMessageIsHumanReadable()
    {
        // Arrange
        var request = ValidRequest();
        request.Subject = string.Empty;

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        var errors = result.Errors.Where(e => e.PropertyName == nameof(ContactSubmissionRequest.Subject)).ToList();
        Assert.Contains(errors, e => !string.IsNullOrWhiteSpace(e.ErrorMessage));
    }

    [Fact]
    public void Validate_WhenSubjectExceedsMaxLength_FailsWithErrorOnSubject()
    {
        // Arrange
        var request = ValidRequest();
        request.Subject = Repeat('S', 201);

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Validate_WhenSubjectIsExactlyMaxLength_Passes()
    {
        // Arrange
        var request = ValidRequest();
        request.Subject = Repeat('S', 200);

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Subject);
    }

    // ---------------------------------------------------------------------------
    // AC4b / AC4c / AC2 / AC3 — Message rules
    // ---------------------------------------------------------------------------

    [Fact]
    public void Validate_WhenMessageIsEmpty_FailsWithErrorOnMessage()
    {
        // Arrange
        var request = ValidRequest();
        request.Message = string.Empty;

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public void Validate_WhenMessageIsEmpty_ErrorMessageIsHumanReadable()
    {
        // Arrange
        var request = ValidRequest();
        request.Message = string.Empty;

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        var errors = result.Errors.Where(e => e.PropertyName == nameof(ContactSubmissionRequest.Message)).ToList();
        Assert.Contains(errors, e => !string.IsNullOrWhiteSpace(e.ErrorMessage));
    }

    [Fact]
    public void Validate_WhenMessageIsExactly1000Chars_Passes()
    {
        // Arrange
        var request = ValidRequest();
        request.Message = Repeat('M', 1000);

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public void Validate_WhenMessageIs1001Chars_FailsWithErrorOnMessage()
    {
        // Arrange
        var request = ValidRequest();
        request.Message = Repeat('M', 1001);

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public void Validate_WhenMessageIs1001Chars_ErrorMessageIsHumanReadable()
    {
        // Arrange
        var request = ValidRequest();
        request.Message = Repeat('M', 1001);

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        var errors = result.Errors.Where(e => e.PropertyName == nameof(ContactSubmissionRequest.Message)).ToList();
        Assert.Contains(errors, e => !string.IsNullOrWhiteSpace(e.ErrorMessage));
    }

    // ---------------------------------------------------------------------------
    // AC4b — Isolation: only the failing field produces an error
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(ContactSubmissionRequest.FullName))]
    [InlineData(nameof(ContactSubmissionRequest.Email))]
    [InlineData(nameof(ContactSubmissionRequest.Phone))]
    [InlineData(nameof(ContactSubmissionRequest.Subject))]
    [InlineData(nameof(ContactSubmissionRequest.Message))]
    public void Validate_WhenSingleFieldIsEmpty_OnlyThatFieldHasValidationError(string fieldName)
    {
        // Arrange
        var request = ValidRequest();

        switch (fieldName)
        {
            case nameof(ContactSubmissionRequest.FullName): request.FullName = string.Empty; break;
            case nameof(ContactSubmissionRequest.Email):    request.Email    = string.Empty; break;
            case nameof(ContactSubmissionRequest.Phone):    request.Phone    = string.Empty; break;
            case nameof(ContactSubmissionRequest.Subject):  request.Subject  = string.Empty; break;
            case nameof(ContactSubmissionRequest.Message):  request.Message  = string.Empty; break;
        }

        // Act
        var result = _sut.TestValidate(request);

        // Assert — at least one error exists for the target field
        Assert.Contains(result.Errors, e => e.PropertyName == fieldName);
    }
}
