using ContactApp.Api.Data;
using ContactApp.Api.Models;
using ContactApp.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace ContactApp.Api.Endpoints;

/// <summary>
/// Registers the contact-related minimal API endpoints.
/// </summary>
public static class ContactEndpoints
{
    public static IEndpointRouteBuilder MapContactEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
           .WithName("Health");

        app.MapPost("/api/contact", HandleContactSubmission)
           .WithName("SubmitContact");

        return app;
    }

    private static async Task<IResult> HandleContactSubmission(
        ContactSubmissionRequest request,
        ContactDbContext db,
        ILogger<Program> logger)
    {
        // Validate
        var errors = ContactValidator.Validate(request);

        if (errors.Count > 0)
        {
            logger.LogWarning(
                "Contact submission validation failed for fields: {Fields}",
                string.Join(", ", errors.Keys));

            return Results.BadRequest(new { errors });
        }

        // Persist
        var submission = new ContactSubmission
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName!.Trim(),
            Email = request.Email!.Trim(),
            Message = request.Message!.Trim(),
            ReceivedAt = DateTime.UtcNow,
        };

        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        logger.LogInformation("Contact submission accepted: {Id}", submission.Id);

        return Results.Created(
            $"/api/contact/{submission.Id}",
            new { id = submission.Id, receivedAt = submission.ReceivedAt });
    }
}
