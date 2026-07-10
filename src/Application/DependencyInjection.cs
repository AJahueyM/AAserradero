using Microsoft.Extensions.DependencyInjection;

namespace AntiguoAserradero.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers all Application-layer feature modules (see <see cref="Abstractions.IServiceModule"/>).
    /// Validators are registered by the API host via assembly scanning.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddServiceModulesFrom(typeof(ApplicationAssemblyMarker).Assembly);
        return services;
    }
}
