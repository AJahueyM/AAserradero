using AntiguoAserradero.Api.Security;
using AntiguoAserradero.Application.Reference;
using AntiguoAserradero.Application.Rooms;

namespace AntiguoAserradero.Api.Endpoints.Rooms;

public sealed class RoomEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/rooms").RequireAuthorization();

        group.MapGet("/", (string? search, int page, int pageSize, IRoomService service, CancellationToken ct) =>
                service.ListAsync(new CatalogListQuery(search, page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize), ct))
            .WithName("ListRooms");

        group.MapGet("/{id:int}", (int id, IRoomService service, CancellationToken ct) => service.GetAsync(id, ct))
            .WithName("GetRoom");

        group.MapPost("/", (UpsertRoomRequest request, IRoomService service, CancellationToken ct) => service.CreateAsync(request, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("CreateRoom");

        group.MapPut("/{id:int}", (int id, UpsertRoomRequest request, IRoomService service, CancellationToken ct) => service.UpdateAsync(id, request, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("UpdateRoom");

        group.MapPost("/{id:int}/deactivate", (int id, IRoomService service, CancellationToken ct) => service.DeactivateAsync(id, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("DeactivateRoom");
    }
}
