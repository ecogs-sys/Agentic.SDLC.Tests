using ContactApp.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// EF Core with Npgsql provider.
// Connection string is read from configuration key ConnectionStrings:Default
// (environment variable: ConnectionStrings__Default)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Health checks (DevOps smoke test + Docker Compose healthcheck at /healthz)
builder.Services.AddHealthChecks();

// OpenAPI / Swagger — enabled only in Development
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply EF Core migrations on startup so the schema is created on first run inside Docker Compose
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapHealthChecks("/healthz");

app.MapControllers();

app.Run();

// Make Program accessible to integration test projects
public partial class Program { }
