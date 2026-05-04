using ContactApp.Api.Data;
using ContactApp.Api.Endpoints;
using ContactApp.Api.Startup;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddDbContext<ContactDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS — origin is configurable via the Cors__AllowedOrigin environment variable
// (which ASP.NET Core maps to the configuration key Cors:AllowedOrigin).
// Falls back to the appsettings.json value, then to the hard-coded localhost default.
var allowedOrigin = builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.WithOrigins(allowedOrigin)
              .WithMethods("GET", "POST", "OPTIONS")
              .WithHeaders("Content-Type");
    });
});

// JSON uses camelCase (System.Text.Json default — no extra config needed).
// No authentication or authorization middleware is registered (REQ-005 / TECH-004).

// ── Pipeline ──────────────────────────────────────────────────────────────────

var app = builder.Build();

// Global exception handler — returns HTTP 500 with a safe error envelope.
// Placed before UseCors so all unhandled exceptions are caught regardless
// of where in the pipeline they originate (AC6).
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    });
});

app.UseCors("frontend");

app.MapContactEndpoints();

// ── Startup migration (retry loop tolerates docker-compose DB startup lag) ───
// Skipped in Testing environment (in-memory provider used by integration tests
// does not support Migrate(); EF in-memory needs no schema setup).

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ContactDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await MigrationHelper.ApplyMigrationsAsync(
        migrateAction: () => db.Database.Migrate(),
        logger: logger);
}

app.Run();

// Expose the implicit Program class so the test project can reference it.
public partial class Program { }
