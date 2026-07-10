namespace AntiguoAserradero.Application.Clients;

public sealed record ClientDto(
    int Id,
    string Name,
    string? TaxId,
    string? Address,
    string? Email,
    string? Phone,
    string Cellphone,
    bool IsVip,
    bool IsBlacklisted,
    string? BlacklistReason,
    bool IsActive,
    int RecentActivityCount);

public sealed record ClientListResponse(
    IReadOnlyList<ClientDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record CreateClientRequest(
    string? Name,
    string? TaxId,
    string? Address,
    string? Email,
    string? Phone,
    string? Cellphone);

public sealed record UpdateClientRequest(
    string? Name,
    string? TaxId,
    string? Address,
    string? Email,
    string? Phone,
    string? Cellphone,
    bool IsVip,
    bool IsBlacklisted,
    string? BlacklistReason);
