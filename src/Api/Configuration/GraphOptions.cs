using System.ComponentModel.DataAnnotations;

namespace AntiguoAserradero.Api.Configuration;

public sealed class GraphOptions
{
    public const string SectionName = "Graph";

    [Required]
    [Url]
    public required string BaseUrl { get; init; }
}
