namespace AntiguoAserradero.Api.Endpoints;

public static class EndpointModuleRegistry
{
    /// <summary>
    /// Discovers every non-abstract <see cref="IEndpointModule"/> in the API assembly and maps it.
    /// </summary>
    public static IEndpointRouteBuilder MapFeatureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        foreach (var type in typeof(EndpointModuleRegistry).Assembly.GetTypes())
        {
            if (type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
                && typeof(IEndpointModule).IsAssignableFrom(type))
            {
                var module = (IEndpointModule)Activator.CreateInstance(type)!;
                module.MapEndpoints(endpoints);
            }
        }

        return endpoints;
    }
}
