namespace AntiguoAserradero.Application.Notifications;

public sealed record ReservationConfirmationRequest(bool IncludePaymentInstructions, bool Compact);

public sealed record ReservationConfirmationContent(string Subject, string HtmlBody, string TextBody, string RecipientEmail);

public sealed record SendReservationConfirmationRequest(bool IncludePaymentInstructions, bool Compact);

public sealed record SendReservationConfirmationResponse(bool Sent, string MessageId, int Attempts);

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);

public sealed record EmailSendResult(bool Succeeded, string MessageId, string? ErrorMessage = null);

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
