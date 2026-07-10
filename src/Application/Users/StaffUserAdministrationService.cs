using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.Auth;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntiguoAserradero.Application.Users;

public sealed class StaffUserAdministrationService : IStaffUserAdministrationService
{
    private static readonly CreateStaffUserRequestValidator CreateValidator = new();
    private static readonly UpdateStaffUserRequestValidator UpdateValidator = new();
    private static readonly StaffUserCapabilityRequestValidator CapabilityValidator = new();
    private static readonly ResetStaffUserPasswordRequestValidator PasswordValidator = new();
    private static readonly Action<ILogger, int, string, string, int, string, DateTime, Exception?> LogSensitiveStaffAction =
        LoggerMessage.Define<int, string, string, int, string, DateTime>(
            LogLevel.Information,
            new EventId(1, nameof(LogSensitiveStaffAction)),
            "Staff user administration: actor {ActorUserId} ({ActorExternalId}) {Action} target {TargetUserId} ({TargetExternalId}) at {UtcTimestamp}.");

    private readonly IApplicationDbContext _dbContext;
    private readonly IStaffDirectory _staffDirectory;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentStaffResolver _staffResolver;
    private readonly ILogger<StaffUserAdministrationService> _logger;

    public StaffUserAdministrationService(
        IApplicationDbContext dbContext,
        IStaffDirectory staffDirectory,
        ICurrentUser currentUser,
        ICurrentStaffResolver staffResolver,
        ILogger<StaffUserAdministrationService> logger)
    {
        _dbContext = dbContext;
        _staffDirectory = staffDirectory;
        _currentUser = currentUser;
        _staffResolver = staffResolver;
        _logger = logger;
    }

    public async Task<StaffUserListResponse> ListAsync(CancellationToken cancellationToken = default)
    {
        EnsureUserAdministrationAllowed();

        var localUsers = await _dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Id)
            .ToListAsync(cancellationToken);

        var directoryUsers = await _staffDirectory.GetUsersByExternalIdsAsync(
            localUsers.Select(user => user.ExternalId).Distinct(StringComparer.Ordinal).ToArray(),
            cancellationToken);

        var items = localUsers
            .Select(user => directoryUsers.TryGetValue(user.ExternalId, out var directoryUser)
                ? ToDto(user, directoryUser)
                : ToDto(user, StaffDirectoryUser.FromLocal(user)))
            .ToArray();

        return new StaffUserListResponse(items);
    }

    public async Task<StaffUserDto> CreateAsync(CreateStaffUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await CreateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var actor = await PrepareSensitiveActionAsync(cancellationToken);

        var email = NormalizeEmail(request.Email!);
        var displayName = NormalizeText(request.DisplayName!);
        var directoryUser = await _staffDirectory.CreateUserAsync(email, displayName, request.InitialPassword!, cancellationToken);
        var localUser = await UpsertLocalUserAsync(directoryUser, cancellationToken);

        LogSensitiveAction("created", actor, localUser);
        return ToDto(localUser, directoryUser);
    }

    public async Task<StaffUserDto> UpdateAsync(int id, UpdateStaffUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await UpdateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var actor = await PrepareSensitiveActionAsync(cancellationToken);
        var localUser = await FindLocalUserAsync(id, cancellationToken);

        var displayName = request.DisplayName is null ? null : NormalizeText(request.DisplayName);
        var directoryUser = await _staffDirectory.UpdateUserAsync(localUser.ExternalId, displayName, request.IsActive, cancellationToken);
        ApplyDirectoryState(localUser, directoryUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        LogSensitiveAction("updated", actor, localUser);
        return ToDto(localUser, directoryUser);
    }

    public async Task<StaffUserDto> AssignCapabilityAsync(int id, StaffUserCapabilityRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await CapabilityValidator.ValidateAndThrowAsync(request, cancellationToken);
        var actor = await PrepareSensitiveActionAsync(cancellationToken);
        var localUser = await FindLocalUserAsync(id, cancellationToken);

        await _staffDirectory.AssignCapabilityAsync(localUser.ExternalId, request.Capability!, cancellationToken);
        var capabilities = await _staffDirectory.GetCapabilitiesAsync(localUser.ExternalId, cancellationToken);

        LogSensitiveAction($"assigned {request.Capability}", actor, localUser);
        return ToDto(localUser, StaffDirectoryUser.FromLocal(localUser, capabilities));
    }

    public async Task<StaffUserDto> RemoveCapabilityAsync(int id, StaffUserCapabilityRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await CapabilityValidator.ValidateAndThrowAsync(request, cancellationToken);
        var actor = await PrepareSensitiveActionAsync(cancellationToken);
        var localUser = await FindLocalUserAsync(id, cancellationToken);

        await _staffDirectory.RemoveCapabilityAsync(localUser.ExternalId, request.Capability!, cancellationToken);
        var capabilities = await _staffDirectory.GetCapabilitiesAsync(localUser.ExternalId, cancellationToken);

        LogSensitiveAction($"removed {request.Capability}", actor, localUser);
        return ToDto(localUser, StaffDirectoryUser.FromLocal(localUser, capabilities));
    }

    public async Task ResetPasswordAsync(int id, ResetStaffUserPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await PasswordValidator.ValidateAndThrowAsync(request, cancellationToken);
        var actor = await PrepareSensitiveActionAsync(cancellationToken);
        var localUser = await FindLocalUserAsync(id, cancellationToken);

        await _staffDirectory.ResetPasswordAsync(localUser.ExternalId, request.NewPassword!, request.ForceChangePasswordNextSignIn, cancellationToken);
        LogSensitiveAction("reset password for", actor, localUser);
    }

    public async Task DisableAsync(int id, CancellationToken cancellationToken = default)
    {
        var actor = await PrepareSensitiveActionAsync(cancellationToken);
        var localUser = await FindLocalUserAsync(id, cancellationToken);

        var directoryUser = await _staffDirectory.UpdateUserAsync(localUser.ExternalId, null, false, cancellationToken);
        ApplyDirectoryState(localUser, directoryUser);
        localUser.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        LogSensitiveAction("disabled", actor, localUser);
    }

    private async Task<User> PrepareSensitiveActionAsync(CancellationToken cancellationToken)
    {
        EnsureUserAdministrationAllowed();
        return await _staffResolver.GetOrCreateAsync(cancellationToken);
    }

    private void EnsureUserAdministrationAllowed()
    {
        if (!_currentUser.Capabilities.Contains(ApplicationCapability.CatalogManage, StringComparer.Ordinal)
            || !_currentUser.Capabilities.Contains(ApplicationCapability.ReservationsManage, StringComparer.Ordinal))
        {
            throw new ForbiddenException("Auth.Forbidden", "User administration requires both capabilities.");
        }
    }

    private async Task<User> FindLocalUserAsync(int id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("User.NotFound", "Staff user was not found.", new { id });
        }

        return user;
    }

    private async Task<User> UpsertLocalUserAsync(StaffDirectoryUser directoryUser, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(
            candidate => candidate.ExternalId == directoryUser.ExternalId || candidate.UserName == directoryUser.UserName,
            cancellationToken);

        if (user is null)
        {
            user = new User
            {
                ExternalId = directoryUser.ExternalId,
                UserName = directoryUser.UserName,
                DisplayName = directoryUser.DisplayName,
                IsActive = directoryUser.IsActive,
            };
            _dbContext.Users.Add(user);
        }
        else
        {
            ApplyDirectoryState(user, directoryUser);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    private static void ApplyDirectoryState(User localUser, StaffDirectoryUser directoryUser)
    {
        localUser.ExternalId = directoryUser.ExternalId;
        localUser.UserName = directoryUser.UserName;
        localUser.DisplayName = directoryUser.DisplayName;
        localUser.IsActive = directoryUser.IsActive;
    }

    private void LogSensitiveAction(string action, User actor, User target)
    {
        LogSensitiveStaffAction(_logger, actor.Id, actor.ExternalId, action, target.Id, target.ExternalId, DateTime.UtcNow, null);
    }

    private static StaffUserDto ToDto(User localUser, StaffDirectoryUser directoryUser)
    {
        return new StaffUserDto(
            localUser.Id,
            string.IsNullOrWhiteSpace(directoryUser.DisplayName) ? localUser.DisplayName : directoryUser.DisplayName,
            string.IsNullOrWhiteSpace(directoryUser.UserName) ? localUser.UserName : directoryUser.UserName,
            directoryUser.IsActive,
            directoryUser.AssignedCapabilities.Where(StaffUserCapabilities.Allowed.Contains).Order(StringComparer.Ordinal).ToArray());
    }

    private static string NormalizeEmail(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string NormalizeText(string value)
    {
        return string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
