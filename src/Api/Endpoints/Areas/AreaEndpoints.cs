using AntiguoAserradero.Api.Security;
using AntiguoAserradero.Application.Areas;
using AntiguoAserradero.Application.Reference;

namespace AntiguoAserradero.Api.Endpoints.Areas;

public sealed class AreaEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/areas").RequireAuthorization();

        group.MapGet("/", (string? search, int page, int pageSize, IAreaService service, CancellationToken ct) =>
                service.ListAsync(new CatalogListQuery(search, page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize), ct))
            .WithName("ListAreas");

        group.MapGet("/{id:int}", (int id, IAreaService service, CancellationToken ct) => service.GetAsync(id, ct))
            .WithName("GetArea");

        group.MapPost("/", (UpsertAreaRequest request, IAreaService service, CancellationToken ct) => service.CreateAsync(request, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("CreateArea");

        group.MapPut("/{id:int}", (int id, UpsertAreaRequest request, IAreaService service, CancellationToken ct) => service.UpdateAsync(id, request, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("UpdateArea");

        group.MapPost("/{id:int}/deactivate", (int id, IAreaService service, CancellationToken ct) => service.DeactivateAsync(id, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("DeactivateArea");
    }
}
