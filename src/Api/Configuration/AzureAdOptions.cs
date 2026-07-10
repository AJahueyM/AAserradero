using System.ComponentModel.DataAnnotations;

namespace AntiguoAserradero.Api.Configuration;

public sealed class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    [Required]
    public required string Instance { get; init; }

    [Required]
    public required string TenantId { get; init; }

    [Required]
    public required string ClientId { get; init; }

    [Required]
    public required string Audience { get; init; }
}
