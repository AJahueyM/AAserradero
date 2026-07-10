namespace AntiguoAserradero.Application.Clients;

public interface IClientService
{
    Task<ClientListResponse> SearchAsync(string? name, bool? isVip, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<ClientDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<ClientDto> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken = default);

    Task<ClientDto> UpdateAsync(int id, UpdateClientRequest request, CancellationToken cancellationToken = default);

    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
