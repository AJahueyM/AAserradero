using AntiguoAserradero.Api.Security;
using AntiguoAserradero.Application.Reservations;

namespace AntiguoAserradero.Api.Endpoints.Reservations;

public sealed class ReservationsEndpointModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reservations").RequireAuthorization();

        group.MapGet("", SearchAsync)
            .WithName("SearchReservations");

        group.MapGet("/{reservationId:int}", GetAsync)
            .WithName("GetReservation");

        group.MapPost("", CreateAsync)
            .RequireAuthorization(AuthorizationPolicyNames.ReservationsManage)
            .WithName("CreateReservation");

        group.MapPut("/{reservationId:int}", UpdateAsync)
            .RequireAuthorization(AuthorizationPolicyNames.ReservationsManage)
            .WithName("UpdateReservation");

        group.MapPost("/{reservationId:int}/cancel", CancelAsync)
            .RequireAuthorization(AuthorizationPolicyNames.ReservationsManage)
            .WithName("CancelReservation");
    }

    private static async Task<IResult> SearchAsync(
        DateTime? from,
        DateTime? to,
        ReservationDateSearchMode? dateMode,
        string? clientPhone,
        string? clientName,
        int? clientId,
        IReservationService service,
        CancellationToken cancellationToken)
    {
        var results = await service.SearchAsync(
            new ReservationSearchRequest(from, to, dateMode ?? ReservationDateSearchMode.Calendar, clientPhone, clientName, clientId),
            cancellationToken);

        return Results.Ok(results);
    }

    private static async Task<IResult> GetAsync(int reservationId, IReservationService service, CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetAsync(reservationId, cancellationToken));
    }

    private static async Task<IResult> CreateAsync(CreateReservationRequest request, IReservationService service, CancellationToken cancellationToken)
    {
        var response = await service.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/reservations/{response.Id}", response);
    }

    private static async Task<IResult> UpdateAsync(int reservationId, UpdateReservationRequest request, IReservationService service, CancellationToken cancellationToken)
    {
        return Results.Ok(await service.UpdateAsync(reservationId, request, cancellationToken));
    }

    private static async Task<IResult> CancelAsync(int reservationId, IReservationService service, CancellationToken cancellationToken)
    {
        await service.CancelAsync(reservationId, cancellationToken);
        return Results.NoContent();
    }
}
