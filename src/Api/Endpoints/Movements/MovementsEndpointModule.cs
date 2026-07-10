using AntiguoAserradero.Api.Security;
using AntiguoAserradero.Application.Movements;

namespace AntiguoAserradero.Api.Endpoints.Movements;

public sealed class MovementsEndpointModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reservations/{reservationId:int}/movements")
            .RequireAuthorization(AuthorizationPolicyNames.ReservationsManage);

        group.MapGet("", ListAsync)
            .WithName("ListReservationMovements");

        group.MapGet("/{movementId:int}", GetAsync)
            .WithName("GetReservationMovement");

        group.MapPost("", AddAsync)
            .WithName("AddReservationMovement");

        group.MapPut("/{movementId:int}", UpdateAsync)
            .WithName("UpdateReservationMovement");

        group.MapDelete("/{movementId:int}", DeleteAsync)
            .WithName("DeleteReservationMovement");

        group.MapPost("/recompute-balance", RecomputeAsync)
            .WithName("RecomputeReservationBalance");
    }

    private static async Task<IResult> ListAsync(int reservationId, IMovementService service, CancellationToken cancellationToken)
    {
        return Results.Ok(await service.ListAsync(reservationId, cancellationToken));
    }

    private static async Task<IResult> GetAsync(int reservationId, int movementId, IMovementService service, CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetAsync(reservationId, movementId, cancellationToken));
    }

    private static async Task<IResult> AddAsync(int reservationId, UpsertMovementRequest request, IMovementService service, CancellationToken cancellationToken)
    {
        return Results.Created($"/api/reservations/{reservationId}/movements", await service.AddAsync(reservationId, request, cancellationToken));
    }

    private static async Task<IResult> UpdateAsync(int reservationId, int movementId, UpsertMovementRequest request, IMovementService service, CancellationToken cancellationToken)
    {
        return Results.Ok(await service.UpdateAsync(reservationId, movementId, request, cancellationToken));
    }

    private static async Task<IResult> DeleteAsync(int reservationId, int movementId, IMovementService service, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(reservationId, movementId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RecomputeAsync(int reservationId, IMovementService service, CancellationToken cancellationToken)
    {
        return Results.Ok(await service.RecomputeReservationAsync(reservationId, cancellationToken));
    }
}
