namespace AntiguoAserradero.Domain.Entities;

public sealed class ConfigValue
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTime UpdatedAt { get; set; }
}
