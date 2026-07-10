using System.ComponentModel.DataAnnotations;

namespace AntiguoAserradero.Api.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";

    [Required]
    [Url]
    public required string FrontendBaseUrl { get; init; }

    [Required]
    [MinLength(1)]
    public required string[] AllowedOrigins { get; init; }
}
