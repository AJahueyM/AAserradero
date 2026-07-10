using System.Security.Claims;
using AntiguoAserradero.Application.Auth;

namespace AntiguoAserradero.Api.Security;

public sealed class CurrentUserAccessor : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Id => Principal?.FindFirstValue("oid") ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub");

    public string DisplayName => Principal?.FindFirstValue("name")
        ?? Principal?.FindFirstValue("preferred_username")
        ?? Principal?.Identity?.Name
        ?? string.Empty;

    public IReadOnlyCollection<string> Capabilities => Principal?.Claims
        .Where(claim => claim.Type is "roles" or ClaimTypes.Role)
        .Select(claim => claim.Value)
        .Where(ApplicationCapability.All.Contains)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray()
        ?? [];

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;
}
