namespace AntiguoAserradero.Domain.Entities;

public sealed class Room
{
    public int Id { get; set; }
    public int AreaId { get; set; }
    public Area? Area { get; set; }
    public required string Name { get; set; }
    public int Capacity { get; set; }
    public int UnitCount { get; set; }
    public decimal NightlyFare { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
