namespace AntiguoAserradero.Domain.Entities;

public sealed class User
{
    public int Id { get; set; }
    public required string ExternalId { get; set; }
    public required string UserName { get; set; }
    public required string DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
}
