using Microsoft.Extensions.DependencyInjection;

namespace AntiguoAserradero.Application.Abstractions;

/// <summary>
/// Implemented by a feature to register its Application/Infrastructure services with DI.
/// Implementations MUST have a public parameterless constructor; they are discovered and
/// instantiated automatically at startup (see <see cref="ModuleRegistration"/>), so each
/// feature adds its own module in its own file with no edits to shared registration code.
/// </summary>
public interface IServiceModule
{
    void Register(IServiceCollection services);
}
