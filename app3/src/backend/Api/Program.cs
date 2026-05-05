using ContactApp.Api.Data;
using ContactApp.Api.Dtos;
using ContactApp.Api.Infrastructure;
using ContactApp.Api.Validators;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Register FluentValidation validators for the controller pipeline.
builder.Services.AddScoped<IValidator<ContactSubmissionRequest>, ContactSubmissionRequestValidator>();

// EF Core with Npgsql provider.
// Connection string is read from configuration key ConnectionStrings:Default
// (environment variable: ConnectionStrings__Default)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// CORS — allow the configured frontend origin (FRONTEND_ORIGIN env var; default http://localhost:3000)
var frontendOrigin = builder.Configuration["FRONTEND_ORIGIN"] ?? "http://localhost:3000";
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins(frontendOrigin)
              .WithMethods("POST")
              .WithHeaders("Content-Type"));
});

// Health checks (DevOps smoke test + Docker Compose healthcheck at /healthz)
builder.Services.AddHealthChecks();

// OpenAPI / Swagger — enabled only in Development
builder.Services.AddOpenApi();

// Global exception handler — returns sanitized JSON (RFC 7807 Problem Details),
// never an HTML stack trace, so the frontend can reliably classify HTTP 500 failures.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// Apply EF Core migrations on startup so the schema is created on first run inside Docker Compose
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to apply database migrations on startup");
    }
}

// Global exception handler must be first in the pipeline.
app.UseExceptionHandler();

// CORS must appear before endpoint routing / controllers.
app.UseCors("FrontendPolicy");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Health check at /healthz only (Docker Compose healthcheck + DevOps smoke test).
// /health alias is not part of the spec; only /healthz is required.
app.MapHealthChecks("/healthz");

app.MapControllers();

app.Run();

// Make Program accessible to integration test projects
public partial class Program { }
