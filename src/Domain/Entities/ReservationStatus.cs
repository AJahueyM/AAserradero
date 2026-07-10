namespace AntiguoAserradero.Domain.Entities;

public sealed class ReservationStatus
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Label { get; set; }
    public int SortOrder { get; set; }
}
