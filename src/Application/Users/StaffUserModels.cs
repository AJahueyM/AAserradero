using AntiguoAserradero.Application.Auth;
using AntiguoAserradero.Domain.Entities;

namespace AntiguoAserradero.Application.Users;

public sealed record StaffUserDto(
    int Id,
    string DisplayName,
    string UserName,
    bool IsActive,
    IReadOnlyList<string> AssignedCapabilities);

public sealed record StaffUserListResponse(IReadOnlyList<StaffUserDto> Items);

public sealed record CreateStaffUserRequest(
    string? Email,
    string? DisplayName,
    string? InitialPassword);

public sealed record UpdateStaffUserRequest(
    string? DisplayName,
    bool? IsActive);

public sealed record StaffUserCapabilityRequest(string? Capability);

public sealed record ResetStaffUserPasswordRequest(
    string? NewPassword,
    bool ForceChangePasswordNextSignIn = true);

public sealed record StaffDirectoryUser(
    string ExternalId,
    string UserName,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<string> AssignedCapabilities)
{
    public static StaffDirectoryUser FromLocal(User user, IReadOnlyList<string>? capabilities = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new StaffDirectoryUser(user.ExternalId, user.UserName, user.DisplayName, user.IsActive, capabilities ?? []);
    }
}

public static class StaffUserCapabilities
{
    public static readonly IReadOnlySet<string> Allowed = ApplicationCapability.All;
}
