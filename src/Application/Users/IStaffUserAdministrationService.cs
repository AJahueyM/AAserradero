namespace AntiguoAserradero.Application.Users;

public interface IStaffUserAdministrationService
{
    Task<StaffUserListResponse> ListAsync(CancellationToken cancellationToken = default);

    Task<StaffUserDto> CreateAsync(CreateStaffUserRequest request, CancellationToken cancellationToken = default);

    Task<StaffUserDto> UpdateAsync(int id, UpdateStaffUserRequest request, CancellationToken cancellationToken = default);

    Task<StaffUserDto> AssignCapabilityAsync(int id, StaffUserCapabilityRequest request, CancellationToken cancellationToken = default);

    Task<StaffUserDto> RemoveCapabilityAsync(int id, StaffUserCapabilityRequest request, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(int id, ResetStaffUserPasswordRequest request, CancellationToken cancellationToken = default);

    Task DisableAsync(int id, CancellationToken cancellationToken = default);
}
