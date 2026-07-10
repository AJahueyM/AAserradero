using AntiguoAserradero.Domain.Entities;

namespace AntiguoAserradero.Application.Abstractions;

/// <summary>
/// Resolves the local <see cref="User"/> row for the authenticated caller, provisioning one on
/// first use (mapping the Entra object id in <see cref="Auth.ICurrentUser.Id"/> to a local user).
/// Used to populate audit/ownership foreign keys such as reservation CreatedBy and movement
/// responsible user.
/// </summary>
public interface ICurrentStaffResolver
{
    Task<User> GetOrCreateAsync(CancellationToken cancellationToken = default);
}
