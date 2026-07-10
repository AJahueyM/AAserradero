namespace AntiguoAserradero.Application.Users;

public interface IStaffDirectory
{
    Task<IReadOnlyDictionary<string, StaffDirectoryUser>> GetUsersByExternalIdsAsync(IReadOnlyCollection<string> externalIds, CancellationToken cancellationToken = default);

    Task<StaffDirectoryUser> CreateUserAsync(string email, string displayName, string initialPassword, CancellationToken cancellationToken = default);

    Task<StaffDirectoryUser> UpdateUserAsync(string externalId, string? displayName, bool? isActive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCapabilitiesAsync(string externalId, CancellationToken cancellationToken = default);

    Task AssignCapabilityAsync(string externalId, string capability, CancellationToken cancellationToken = default);

    Task RemoveCapabilityAsync(string externalId, string capability, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(string externalId, string newPassword, bool forceChangePasswordNextSignIn, CancellationToken cancellationToken = default);
}
