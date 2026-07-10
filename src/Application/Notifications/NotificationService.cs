using System.Globalization;
using System.Net;
using System.Text;
using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.Notifications;

public interface INotificationService
{
    Task<ReservationConfirmationContent> RenderReservationConfirmationAsync(int reservationId, ReservationConfirmationRequest request, CancellationToken cancellationToken = default);

    Task<SendReservationConfirmationResponse> SendReservationConfirmationAsync(int reservationId, SendReservationConfirmationRequest request, CancellationToken cancellationToken = default);
}

public sealed class NotificationService : INotificationService
{
    private const int MaxSendAttempts = 3;
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-MX");
    private readonly IApplicationDbContext _dbContext;
    private readonly IEmailSender _emailSender;

    public NotificationService(IApplicationDbContext dbContext, IEmailSender emailSender)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
    }

    public async Task<ReservationConfirmationContent> RenderReservationConfirmationAsync(int reservationId, ReservationConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .Include(item => item.Client)
            .Include(item => item.Room).ThenInclude(room => room!.Area)
            .Include(item => item.Movements).ThenInclude(movement => movement.Concept)
            .Include(item => item.Movements).ThenInclude(movement => movement.PaymentMethod)
            .FirstOrDefaultAsync(item => item.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            throw new NotFoundException("Notifications.Reservation.NotFound", "No se encontró la reservación solicitada.", new { reservationId });
        }

        if (string.IsNullOrWhiteSpace(reservation.Client?.Email))
        {
            throw new ValidationException("Notifications.Email.MissingRecipient", "El cliente no tiene un correo electrónico registrado.", new { reservationId });
        }

        var paymentInstructions = request.IncludePaymentInstructions
            ? await _dbContext.ConfigValues.AsNoTracking()
                .Where(value => value.Key == ConfigKeys.PaymentInstructions)
                .Select(value => value.Value)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var movements = reservation.Movements.OrderBy(movement => movement.Date).ThenBy(movement => movement.Id).ToArray();
        var charges = movements.Sum(movement => movement.Charge);
        var payments = movements.Sum(movement => movement.Payment);
        var balance = charges - payments;
        var subject = $"Confirmación de reservación #{reservation.Id} - Antiguo Aserradero Reserva";

        var html = new StringBuilder();
        html.Append("<html><body>");
        html.Append("<h1>Confirmación de reservación</h1>");
        AppendParagraph(html, "Referencia", $"#{reservation.Id}");
        AppendParagraph(html, "Cliente", reservation.Client.Name);
        AppendParagraph(html, "Contacto", $"{reservation.Client.Email} / {reservation.Client.Cellphone}");
        AppendParagraph(html, "Área / habitación", $"{reservation.Room?.Area?.Name} / {reservation.Room?.Name}");
        AppendParagraph(html, "Estancia", $"{FormatDate(reservation.EntryDate)} {reservation.CheckInTime} - {FormatDate(reservation.ExitDate)} {reservation.CheckOutTime}");
        AppendParagraph(html, "Ocupantes", $"{reservation.Adults} adultos, {reservation.Children} menores, {reservation.Infants} infantes, {reservation.Pets} mascotas");

        if (!request.Compact && !string.IsNullOrWhiteSpace(reservation.Notes))
        {
            AppendParagraph(html, "Descripción", reservation.Notes);
        }

        html.Append("<h2>Estado de cuenta</h2><table><thead><tr><th>Fecha</th><th>Concepto</th><th>Cargo</th><th>Pago</th></tr></thead><tbody>");
        foreach (var movement in movements)
        {
            html.Append("<tr><td>")
                .Append(E(FormatDate(movement.Date)))
                .Append("</td><td>")
                .Append(E(movement.Concept?.Name ?? "Movimiento"))
                .Append("</td><td>")
                .Append(E(FormatMoney(movement.Charge)))
                .Append("</td><td>")
                .Append(E(FormatMoney(movement.Payment)))
                .Append("</td></tr>");
        }

        html.Append("</tbody></table>");
        AppendParagraph(html, "Saldo pendiente", FormatMoney(balance));

        if (request.IncludePaymentInstructions && !string.IsNullOrWhiteSpace(paymentInstructions))
        {
            AppendParagraph(html, "Instrucciones de pago", paymentInstructions);
        }

        html.Append("</body></html>");

        var text = new StringBuilder();
        text.AppendLine("Confirmación de reservación");
        text.AppendLine(Culture, $"Referencia: #{reservation.Id}");
        text.AppendLine(Culture, $"Cliente: {reservation.Client.Name}");
        text.AppendLine(Culture, $"Contacto: {reservation.Client.Email} / {reservation.Client.Cellphone}");
        text.AppendLine(Culture, $"Área / habitación: {reservation.Room?.Area?.Name} / {reservation.Room?.Name}");
        text.AppendLine(Culture, $"Estancia: {FormatDate(reservation.EntryDate)} {reservation.CheckInTime} - {FormatDate(reservation.ExitDate)} {reservation.CheckOutTime}");
        text.AppendLine(Culture, $"Ocupantes: {reservation.Adults} adultos, {reservation.Children} menores, {reservation.Infants} infantes, {reservation.Pets} mascotas");
        if (!request.Compact && !string.IsNullOrWhiteSpace(reservation.Notes))
        {
            text.AppendLine(Culture, $"Descripción: {reservation.Notes}");
        }

        text.AppendLine("Estado de cuenta:");
        foreach (var movement in movements)
        {
            text.AppendLine(Culture, $"- {FormatDate(movement.Date)} {movement.Concept?.Name ?? "Movimiento"} Cargo {FormatMoney(movement.Charge)} Pago {FormatMoney(movement.Payment)}");
        }

        text.AppendLine(Culture, $"Saldo pendiente: {FormatMoney(balance)}");
        if (request.IncludePaymentInstructions && !string.IsNullOrWhiteSpace(paymentInstructions))
        {
            text.AppendLine(Culture, $"Instrucciones de pago: {paymentInstructions}");
        }

        return new ReservationConfirmationContent(subject, html.ToString(), text.ToString(), reservation.Client.Email);
    }

    public async Task<SendReservationConfirmationResponse> SendReservationConfirmationAsync(int reservationId, SendReservationConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        var content = await RenderReservationConfirmationAsync(
            reservationId,
            new ReservationConfirmationRequest(request.IncludePaymentInstructions, request.Compact),
            cancellationToken);

        EmailSendResult result = new(false, string.Empty, "No se intentó el envío.");
        for (var attempt = 1; attempt <= MaxSendAttempts; attempt++)
        {
            result = await _emailSender.SendAsync(new EmailMessage(content.RecipientEmail, content.Subject, content.HtmlBody, content.TextBody), cancellationToken);
            if (result.Succeeded)
            {
                return new SendReservationConfirmationResponse(true, result.MessageId, attempt);
            }
        }

        throw new ConflictException("Notifications.Email.SendFailed", "No fue posible enviar el correo de confirmación.", new { result.ErrorMessage });
    }

    private static void AppendParagraph(StringBuilder html, string label, string? value)
    {
        html.Append("<p><strong>")
            .Append(E(label))
            .Append(":</strong> ")
            .Append(E(value ?? string.Empty))
            .Append("</p>");
    }

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static string FormatDate(DateTime value) => value.ToString("d 'de' MMMM 'de' yyyy", Culture);

    private static string FormatMoney(decimal value) => value.ToString("C", Culture);
}
