using System.ComponentModel.DataAnnotations;

namespace AntiguoAserradero.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required]
    public required string Default { get; init; }
}
