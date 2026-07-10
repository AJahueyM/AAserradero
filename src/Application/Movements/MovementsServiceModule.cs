using AntiguoAserradero.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AntiguoAserradero.Application.Movements;

public sealed class MovementsServiceModule : IServiceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddScoped<IMovementService, MovementService>();
    }
}
