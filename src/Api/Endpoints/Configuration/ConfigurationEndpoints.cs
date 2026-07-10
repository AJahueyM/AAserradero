using AntiguoAserradero.Api.Security;
using AntiguoAserradero.Application.Configuration;

namespace AntiguoAserradero.Api.Endpoints.Configuration;

public sealed class ConfigurationEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/config").RequireAuthorization();

        group.MapGet("/", (IConfigValueService service, CancellationToken ct) => service.ListAsync(ct))
            .WithName("ListConfigValues");

        group.MapGet("/{key}", (string key, IConfigValueService service, CancellationToken ct) => service.GetAsync(key, ct))
            .WithName("GetConfigValue");

        group.MapPut("/{key}", (string key, UpdateConfigValueRequest request, IConfigValueService service, CancellationToken ct) => service.UpdateAsync(key, request, ct))
            .RequireAuthorization(AuthorizationPolicyNames.CatalogManage)
            .WithName("UpdateConfigValue");
    }
}
