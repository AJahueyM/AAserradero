using System.Text.Json;
using System.Threading.RateLimiting;
using AntiguoAserradero.Api.Configuration;
using AntiguoAserradero.Api.Endpoints;
using AntiguoAserradero.Api.Extensions;
using AntiguoAserradero.Api.Middleware;
using AntiguoAserradero.Api.Security;
using AntiguoAserradero.Api.Serialization;
using AntiguoAserradero.Application;
using AntiguoAserradero.Application.Auth;
using AntiguoAserradero.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Identity.Web;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .WriteTo.Console(new RenderedCompactJsonFormatter());

    var applicationInsightsConnectionString = context.Configuration.GetSection(ApplicationInsightsOptions.SectionName).GetValue<string>("ConnectionString");
    if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString) &&
        !applicationInsightsConnectionString.Contains("00000000-0000-0000-0000-000000000000", StringComparison.Ordinal))
    {
        loggerConfiguration.WriteTo.ApplicationInsights(applicationInsightsConnectionString, new TraceTelemetryConverter());
    }
});

builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
builder.Services.AddValidatedOptions(builder.Configuration);
builder.Services.AddInfrastructure();
builder.Services.AddApplication();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
builder.Services.AddValidatorsFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = false;
    options.SerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        var appOptions = builder.Configuration.GetSection(AppOptions.SectionName).Get<AppOptions>()
            ?? throw new InvalidOperationException("App configuration is required.");
        policy.WithOrigins(appOptions.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection(AzureAdOptions.SectionName));

builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Events ??= new JwtBearerEvents();

    // Browser EventSource cannot send an Authorization header, so the SSE endpoint receives the
    // token as an access_token query-string parameter. Read it only for that path.
    var existingMessageReceived = options.Events.OnMessageReceived;
    options.Events.OnMessageReceived = async context =>
    {
        if (existingMessageReceived is not null)
        {
            await existingMessageReceived(context);
        }

        if (string.IsNullOrEmpty(context.Token))
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken) &&
                context.HttpContext.Request.Path.StartsWithSegments("/api/events"))
            {
                context.Token = accessToken;
            }
        }
    };

    var existingChallenge = options.Events.OnChallenge;
    options.Events.OnChallenge = async context =>
    {
        if (existingChallenge is not null)
        {
            await existingChallenge(context);
        }

        if (context.Handled)
        {
            return;
        }

        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            new ErrorEnvelope(new ErrorBody("Auth.Unauthorized", "Authentication is required.")),
            ErrorJsonContext.Default.ErrorEnvelope,
            context.HttpContext.RequestAborted);
    };

    var existingForbidden = options.Events.OnForbidden;
    options.Events.OnForbidden = async context =>
    {
        if (existingForbidden is not null)
        {
            await existingForbidden(context);
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            new ErrorEnvelope(new ErrorBody("Auth.Forbidden", "The authenticated user is not permitted to perform this action.")),
            ErrorJsonContext.Default.ErrorEnvelope,
            context.HttpContext.RequestAborted);
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicyNames.CatalogManage, policy => policy.RequireClaim("roles", ApplicationCapability.CatalogManage));
    options.AddPolicy(AuthorizationPolicyNames.ReservationsManage, policy => policy.RequireClaim("roles", ApplicationCapability.ReservationsManage));
});

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors("ConfiguredOrigins");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Liveness/readiness probe used by Azure Container Apps health checks.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    .WithName("Health");

app.MapMeEndpoints();
app.MapEventsEndpoints();
app.MapFeatureEndpoints();
app.MapProductionSpaFallback();

app.Run();

// Exposed so integration tests can reference the entry-point assembly via WebApplicationFactory.
namespace AntiguoAserradero.Api
{
    public partial class Program;
}
