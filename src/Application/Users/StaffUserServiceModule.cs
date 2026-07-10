using AntiguoAserradero.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AntiguoAserradero.Application.Users;

public sealed class StaffUserServiceModule : IServiceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddScoped<IStaffUserAdministrationService, StaffUserAdministrationService>();
    }
}
