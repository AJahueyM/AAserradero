using System.ComponentModel.DataAnnotations;

namespace AntiguoAserradero.Infrastructure.Identity;

public sealed class GraphOptions
{
    public const string SectionName = "Graph";

    [Required]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    public string ApiClientAppId { get; set; } = string.Empty;
}
