using AntiguoAserradero.Application.Auth;

namespace AntiguoAserradero.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/me", (ICurrentUser currentUser) => Results.Ok(new MeResponse(
                currentUser.Id ?? string.Empty,
                currentUser.DisplayName,
                currentUser.Capabilities.ToArray())))
            .RequireAuthorization()
            .WithName("GetMe");

        return endpoints;
    }

    private sealed record MeResponse(string Id, string DisplayName, string[] Capabilities);
}
