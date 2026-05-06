using ContactApp.Api.Dtos;
using FluentValidation;

namespace ContactApp.Api.Validators;

/// <summary>
/// FluentValidation validator for <see cref="ContactSubmissionRequest"/>.
/// Enforces length and format rules per TECH-002.
/// </summary>
public sealed class ContactSubmissionRequestValidator : AbstractValidator<ContactSubmissionRequest>
{
    public ContactSubmissionRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
                .WithMessage("Full name is required.")
            .MaximumLength(200)
                .WithMessage("Full name must be 200 characters or fewer.");

        RuleFor(x => x.Email)
            .NotEmpty()
                .WithMessage("Email is required.")
            .EmailAddress()
                .WithMessage("Email is not a valid format.");

        RuleFor(x => x.Phone)
            .NotEmpty()
                .WithMessage("Phone is required.")
            .MaximumLength(50)
                .WithMessage("Phone must be 50 characters or fewer.");

        RuleFor(x => x.Subject)
            .NotEmpty()
                .WithMessage("Subject is required.")
            .MaximumLength(200)
                .WithMessage("Subject must be 200 characters or fewer.");

        RuleFor(x => x.Message)
            .NotEmpty()
                .WithMessage("Message is required.")
            .MaximumLength(1000)
                .WithMessage("Message must be 1000 characters or fewer.");
    }
}
