using AntiguoAserradero.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AntiguoAserradero.Application.Reservations;

public sealed class ReservationsServiceModule : IServiceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddScoped<IReservationFinancialService, ReservationFinancialService>();
        services.AddScoped<IReservationLiveUpdateNotifier, ReservationLiveUpdateNotifier>();
        services.AddScoped<IReservationService, ReservationService>();
    }
}
