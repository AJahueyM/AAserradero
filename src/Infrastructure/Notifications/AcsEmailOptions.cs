using System.ComponentModel.DataAnnotations;

namespace AntiguoAserradero.Infrastructure.Notifications;

public sealed class AcsEmailOptions
{
    public const string SectionName = "Acs";

    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string SenderAddress { get; set; } = string.Empty;
}
