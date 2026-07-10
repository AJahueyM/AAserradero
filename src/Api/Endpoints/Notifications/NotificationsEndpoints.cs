using AntiguoAserradero.Api.Security;
using AntiguoAserradero.Application.Notifications;

namespace AntiguoAserradero.Api.Endpoints.Notifications;

public sealed class NotificationsEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications/reservations/{reservationId:int}")
            .RequireAuthorization(AuthorizationPolicyNames.ReservationsManage);

        group.MapPost("/confirmation/preview", async (int reservationId, ReservationConfirmationRequest request, INotificationService notifications, CancellationToken cancellationToken) =>
            Results.Ok(await notifications.RenderReservationConfirmationAsync(reservationId, request, cancellationToken)))
            .WithName("PreviewReservationConfirmation");

        group.MapPost("/confirmation/send", async (int reservationId, SendReservationConfirmationRequest request, INotificationService notifications, CancellationToken cancellationToken) =>
            Results.Ok(await notifications.SendReservationConfirmationAsync(reservationId, request, cancellationToken)))
            .WithName("SendReservationConfirmation");
    }
}
