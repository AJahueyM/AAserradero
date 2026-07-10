using AntiguoAserradero.Api.Security;
using AntiguoAserradero.Application.Clients;

namespace AntiguoAserradero.Api.Endpoints.Clients;

public sealed class ClientsEndpointModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/clients")
            .RequireAuthorization()
            .WithTags("Clients");

        group.MapGet("", SearchClientsAsync)
            .WithName("SearchClients");

        group.MapGet("/{id:int}", GetClientAsync)
            .WithName("GetClient");

        group.MapPost("", CreateClientAsync)
            .RequireAuthorization(AuthorizationPolicyNames.ReservationsManage)
            .WithName("CreateClient");

        group.MapPut("/{id:int}", UpdateClientAsync)
            .RequireAuthorization(AuthorizationPolicyNames.ReservationsManage)
            .WithName("UpdateClient");

        group.MapDelete("/{id:int}", DeactivateClientAsync)
            .RequireAuthorization(AuthorizationPolicyNames.ReservationsManage)
            .WithName("DeactivateClient");
    }

    private static async Task<IResult> SearchClientsAsync(
        IClientService clientService,
        string? name,
        bool? isVip,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await clientService.SearchAsync(name, isVip, page, pageSize, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetClientAsync(IClientService clientService, int id, CancellationToken cancellationToken)
    {
        var response = await clientService.GetAsync(id, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateClientAsync(IClientService clientService, CreateClientRequest request, CancellationToken cancellationToken)
    {
        var response = await clientService.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/clients/{response.Id}", response);
    }

    private static async Task<IResult> UpdateClientAsync(IClientService clientService, int id, UpdateClientRequest request, CancellationToken cancellationToken)
    {
        var response = await clientService.UpdateAsync(id, request, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> DeactivateClientAsync(IClientService clientService, int id, CancellationToken cancellationToken)
    {
        await clientService.DeactivateAsync(id, cancellationToken);
        return Results.NoContent();
    }
}

