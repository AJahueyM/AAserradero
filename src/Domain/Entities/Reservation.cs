namespace AntiguoAserradero.Domain.Entities;

public sealed class Reservation
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client? Client { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public DateTime EntryDate { get; set; }
    public DateTime ExitDate { get; set; }
    public TimeOnly CheckInTime { get; set; }
    public TimeOnly CheckOutTime { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public int Infants { get; set; }
    public int Pets { get; set; }
    public decimal Fare { get; set; }
    public int StatusId { get; set; }
    public ReservationStatus? Status { get; set; }
    public int PromotorId { get; set; }
    public User? Promotor { get; set; }
    public string? Notes { get; set; }
    public int CreatedById { get; set; }
    public User? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Movement> Movements { get; } = new List<Movement>();
}
