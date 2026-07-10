using System.ComponentModel.DataAnnotations;

namespace AntiguoAserradero.Api.Configuration;

public sealed class AcsOptions
{
    public const string SectionName = "Acs";

    [Required]
    [Url]
    public required string Endpoint { get; init; }

    [Required]
    public required string SenderAddress { get; init; }
}
