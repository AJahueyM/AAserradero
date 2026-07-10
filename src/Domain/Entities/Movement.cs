namespace AntiguoAserradero.Domain.Entities;

public sealed class Movement
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    public int ConceptId { get; set; }
    public Concept? Concept { get; set; }
    public int? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public int? PaymentLocationId { get; set; }
    public PaymentLocation? PaymentLocation { get; set; }
    public decimal Charge { get; set; }
    public decimal Payment { get; set; }
    public DateTime Date { get; set; }
    public int ResponsibleUserId { get; set; }
    public User? ResponsibleUser { get; set; }
    public DateTime CreatedAt { get; set; }
}
