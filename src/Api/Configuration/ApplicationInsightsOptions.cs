using System.ComponentModel.DataAnnotations;

namespace AntiguoAserradero.Api.Configuration;

public sealed class ApplicationInsightsOptions
{
    public const string SectionName = "ApplicationInsights";

    [Required]
    public required string ConnectionString { get; init; }
}
