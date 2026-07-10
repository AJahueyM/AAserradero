var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness/readiness probe used by Azure Container Apps health checks.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health");

app.Run();

// Exposed so integration tests can reference the entry-point assembly via WebApplicationFactory.
namespace AntiguoAserradero.Api
{
    public partial class Program;
}
