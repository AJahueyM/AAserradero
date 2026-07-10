using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.Auth;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Infrastructure.Identity;

public sealed class CurrentStaffResolver : ICurrentStaffResolver
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public CurrentStaffResolver(IApplicationDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<User> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var externalId = _currentUser.Id;
        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new UnauthorizedException("Auth.Unauthenticated", "No authenticated user is available.");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(candidate => candidate.ExternalId == externalId, cancellationToken);

        if (user is not null)
        {
            return user;
        }

        var displayName = string.IsNullOrWhiteSpace(_currentUser.DisplayName) ? externalId : _currentUser.DisplayName;
        user = new User
        {
            ExternalId = externalId,
            UserName = displayName,
            DisplayName = displayName,
            IsActive = true,
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }
}
