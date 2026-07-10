using AntiguoAserradero.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AntiguoAserradero.Application.Notifications;

public sealed class NotificationsServiceModule : IServiceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
    }
}
