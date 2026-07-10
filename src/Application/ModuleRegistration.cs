using System.Reflection;
using AntiguoAserradero.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AntiguoAserradero.Application;

public static class ModuleRegistration
{
    /// <summary>
    /// Discovers every non-abstract <see cref="IServiceModule"/> in the given assembly and lets
    /// it register its services. This keeps feature registration modular and collision-free.
    /// </summary>
    public static IServiceCollection AddServiceModulesFrom(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var type in assembly.GetTypes())
        {
            if (type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
                && typeof(IServiceModule).IsAssignableFrom(type))
            {
                var module = (IServiceModule)Activator.CreateInstance(type)!;
                module.Register(services);
            }
        }

        return services;
    }
}
