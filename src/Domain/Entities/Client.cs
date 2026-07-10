namespace AntiguoAserradero.Domain.Entities;

public sealed class Client
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public required string Cellphone { get; set; }
    public bool IsVip { get; set; }
    public bool IsBlacklisted { get; set; }
    public string? BlacklistReason { get; set; }
    public bool IsActive { get; set; } = true;
}
