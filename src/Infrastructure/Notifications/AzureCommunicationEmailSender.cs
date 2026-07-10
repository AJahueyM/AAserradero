using AntiguoAserradero.Application.Notifications;
using Azure;
using Azure.Communication.Email;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AppEmailMessage = AntiguoAserradero.Application.Notifications.EmailMessage;
using AppEmailSendResult = AntiguoAserradero.Application.Notifications.EmailSendResult;

namespace AntiguoAserradero.Infrastructure.Notifications;

public sealed class AzureCommunicationEmailSender : IEmailSender
{
    private static readonly Action<ILogger, string, string, Exception?> LogEmailSent =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1, nameof(LogEmailSent)), "Sent reservation confirmation email to {Recipient} with ACS message {MessageId}.");

    private static readonly Action<ILogger, string, string?, Exception?> LogEmailFailed =
        LoggerMessage.Define<string, string?>(LogLevel.Warning, new EventId(2, nameof(LogEmailFailed)), "Failed to send reservation confirmation email to {Recipient}: {ErrorMessage}");

    private readonly AcsEmailOptions _options;
    private readonly ILogger<AzureCommunicationEmailSender> _logger;

    public AzureCommunicationEmailSender(IOptions<AcsEmailOptions> options, ILogger<AzureCommunicationEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AppEmailSendResult> SendAsync(AppEmailMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new EmailClient(new Uri(_options.Endpoint), new DefaultAzureCredential());
            var content = new EmailContent(message.Subject)
            {
                Html = message.HtmlBody,
                PlainText = message.TextBody,
            };
            var recipients = new EmailRecipients([new EmailAddress(message.To)]);
            var email = new Azure.Communication.Email.EmailMessage(_options.SenderAddress, recipients, content);
            var operation = await client.SendAsync(WaitUntil.Completed, email, cancellationToken);
            LogEmailSent(_logger, message.To, operation.Id, null);
            return new AppEmailSendResult(true, operation.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogEmailFailed(_logger, message.To, exception.Message, exception);
            return new AppEmailSendResult(false, string.Empty, exception.Message);
        }
    }
}
