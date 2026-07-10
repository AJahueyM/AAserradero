using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AntiguoAserradero.Infrastructure.Identity;

public sealed class StaffDirectoryServiceModule : IServiceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddOptions<GraphOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.TenantId = configuration[$"{GraphOptions.SectionName}:TenantId"] ?? string.Empty;
                options.ApiClientAppId = configuration[$"{GraphOptions.SectionName}:ApiClientAppId"] ?? string.Empty;
            })
            .ValidateDataAnnotations();
        services.AddScoped<IStaffDirectory, MicrosoftGraphStaffDirectory>();
    }
}
