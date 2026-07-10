using AntiguoAserradero.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AntiguoAserradero.Application.Clients;

public sealed class ClientsServiceModule : IServiceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddScoped<IClientService, ClientService>();
    }
}
