using ContactApp.Api.Data;
using ContactApp.Api.Data.Entities;
using ContactApp.Api.Dtos;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ContactApp.Api.Controllers;

/// <summary>
/// Handles the public anonymous POST /api/contact endpoint for contact form submissions.
/// No [Authorize] attribute is applied; no auth middleware covers this endpoint.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IValidator<ContactSubmissionRequest> _validator;
    private readonly ILogger<ContactController> _logger;

    public ContactController(
        AppDbContext db,
        IValidator<ContactSubmissionRequest> validator,
        ILogger<ContactController> logger)
    {
        _db = db;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Submit a contact form. Validates the request, persists a ContactSubmission,
    /// and returns 201 with { id, receivedAt } on success,
    /// or 400 with { errors: { camelCaseField: [messages] } } on validation failure.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] ContactSubmissionRequest request)
    {
        // Run FluentValidation manually so we control the error response shape.
        var validationResult = await _validator.ValidateAsync(request);

        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Contact submission validation failed: {Errors}",
                string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));

            // Build camelCase keyed errors dictionary to match the frontend contract (TECH-005).
            var errors = validationResult.Errors
                .GroupBy(e => ToCamelCase(e.PropertyName))
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            return BadRequest(new { errors });
        }

        var submission = new ContactSubmission
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            Subject = request.Subject,
            Message = request.Message,
            ReceivedAt = DateTime.UtcNow
        };

        _db.ContactSubmissions.Add(submission);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Contact submission persisted: id={Id}", submission.Id);

        return StatusCode(StatusCodes.Status201Created, new
        {
            id = submission.Id,
            receivedAt = submission.ReceivedAt
        });
    }

    /// <summary>
    /// Converts a PascalCase property name (e.g. "FullName") to camelCase (e.g. "fullName").
    /// FluentValidation returns property names in PascalCase matching the C# property names.
    /// </summary>
    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return propertyName;

        return char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
    }
}
