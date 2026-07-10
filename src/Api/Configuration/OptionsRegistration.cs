using AntiguoAserradero.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace AntiguoAserradero.Api.Configuration;

public static class OptionsRegistration
{
    public static IServiceCollection AddValidatedOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName);
        services.AddValidatedOptions<AzureAdOptions>(configuration, AzureAdOptions.SectionName);
        services.AddValidatedOptions<GraphOptions>(configuration, GraphOptions.SectionName);
        services.AddValidatedOptions<AcsOptions>(configuration, AcsOptions.SectionName);
        services.AddValidatedOptions<AppOptions>(configuration, AppOptions.SectionName)
            .Validate(options => options.AllowedOrigins.All(origin => Uri.TryCreate(origin, UriKind.Absolute, out _)),
                "Every App:AllowedOrigins entry must be an absolute URI.");
        services.AddValidatedOptions<ApplicationInsightsOptions>(configuration, ApplicationInsightsOptions.SectionName);

        return services;
    }

    private static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(this IServiceCollection services, IConfiguration configuration, string sectionName)
        where TOptions : class
    {
        return services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
