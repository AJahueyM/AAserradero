namespace AntiguoAserradero.Domain.Entities;

public sealed class Concept
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsDiscount { get; set; }
    public bool IsProtected { get; set; }
    public bool IsActive { get; set; } = true;
}
