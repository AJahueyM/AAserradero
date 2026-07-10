namespace AntiguoAserradero.Domain.Entities;

public sealed class Area
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public TimeOnly CheckInTime { get; set; }
    public TimeOnly CheckOutTime { get; set; }
    public TimeOnly ReceptionOpenTime { get; set; }
    public TimeOnly ReceptionCloseTime { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Room> Rooms { get; } = new List<Room>();
}
