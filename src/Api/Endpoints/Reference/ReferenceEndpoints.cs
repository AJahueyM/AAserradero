using AntiguoAserradero.Api.Security;
using AntiguoAserradero.Application.Reference;

namespace AntiguoAserradero.Api.Endpoints.Reference;

public sealed class ReferenceEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var methods = endpoints.MapGroup("/api/payment-methods").RequireAuthorization();
        methods.MapGet("/", (string? search, int page, int pageSize, IReferenceDataService service, CancellationToken ct) =>
                service.ListPaymentMethodsAsync(new CatalogListQuery(search, page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize), ct))
            .WithName("ListPaymentMethods");
        methods.MapPost("/", (UpsertReferenceItemRequest request, IReferenceDataService service, CancellationToken ct) => service.CreatePaymentMethodAsync(request, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("CreatePaymentMethod");
        methods.MapPut("/{id:int}", (int id, UpsertReferenceItemRequest request, IReferenceDataService service, CancellationToken ct) => service.UpdatePaymentMethodAsync(id, request, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("UpdatePaymentMethod");
        methods.MapPost("/{id:int}/deactivate", (int id, IReferenceDataService service, CancellationToken ct) => service.DeactivatePaymentMethodAsync(id, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("DeactivatePaymentMethod");

        var locations = endpoints.MapGroup("/api/payment-locations").RequireAuthorization();
        locations.MapGet("/", (string? search, int page, int pageSize, IReferenceDataService service, CancellationToken ct) =>
                service.ListPaymentLocationsAsync(new CatalogListQuery(search, page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize), ct))
            .WithName("ListPaymentLocations");
        locations.MapPost("/", (UpsertReferenceItemRequest request, IReferenceDataService service, CancellationToken ct) => service.CreatePaymentLocationAsync(request, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("CreatePaymentLocation");
        locations.MapPut("/{id:int}", (int id, UpsertReferenceItemRequest request, IReferenceDataService service, CancellationToken ct) => service.UpdatePaymentLocationAsync(id, request, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("UpdatePaymentLocation");
        locations.MapPost("/{id:int}/deactivate", (int id, IReferenceDataService service, CancellationToken ct) => service.DeactivatePaymentLocationAsync(id, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("DeactivatePaymentLocation");

        endpoints.MapGet("/api/reservation-statuses", (IReferenceDataService service, CancellationToken ct) => service.ListReservationStatusesAsync(ct))
            .RequireAuthorization()
            .WithName("ListReservationStatuses");

        endpoints.MapGet("/api/users/lookup", (string? search, int page, int pageSize, IReferenceDataService service, CancellationToken ct) =>
                service.ListUsersAsync(new CatalogListQuery(search, page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize), ct))
            .RequireAuthorization()
            .WithName("ListUserLookup");
    }
}
