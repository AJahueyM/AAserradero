using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AntiguoAserradero.Infrastructure.Notifications;

public sealed class NotificationsInfrastructureServiceModule : IServiceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddOptions<AcsEmailOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.Endpoint = configuration[$"{AcsEmailOptions.SectionName}:Endpoint"] ?? string.Empty;
                options.SenderAddress = configuration[$"{AcsEmailOptions.SectionName}:SenderAddress"] ?? string.Empty;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IEmailSender, AzureCommunicationEmailSender>();
    }
}
