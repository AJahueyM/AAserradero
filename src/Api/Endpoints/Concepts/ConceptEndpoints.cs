using AntiguoAserradero.Api.Security;
using AntiguoAserradero.Application.Concepts;
using AntiguoAserradero.Application.Reference;

namespace AntiguoAserradero.Api.Endpoints.Concepts;

public sealed class ConceptEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/concepts").RequireAuthorization();

        group.MapGet("/", (string? search, int page, int pageSize, IConceptService service, CancellationToken ct) =>
                service.ListAsync(new CatalogListQuery(search, page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize), ct))
            .WithName("ListConcepts");

        group.MapPost("/", (UpsertConceptRequest request, IConceptService service, CancellationToken ct) => service.CreateAsync(request, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("CreateConcept");

        group.MapPut("/{id:int}", (int id, UpsertConceptRequest request, IConceptService service, CancellationToken ct) => service.UpdateAsync(id, request, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("UpdateConcept");

        group.MapPost("/{id:int}/deactivate", (int id, IConceptService service, CancellationToken ct) => service.DeactivateAsync(id, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("DeactivateConcept");
    }
}
