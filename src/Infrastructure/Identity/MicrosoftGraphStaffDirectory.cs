using AntiguoAserradero.Application.Auth;
using AntiguoAserradero.Application.Users;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using GraphUser = Microsoft.Graph.Models.User;

namespace AntiguoAserradero.Infrastructure.Identity;

public sealed class MicrosoftGraphStaffDirectory : IStaffDirectory
{
    private static readonly Action<ILogger, string, DateTime, Exception?> LogStaffIdentityCreated =
        LoggerMessage.Define<string, DateTime>(
            LogLevel.Information,
            new EventId(1, nameof(LogStaffIdentityCreated)),
            "Created staff identity {ExternalId} in Microsoft Graph at {UtcTimestamp}.");

    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];
    private static readonly string[] UserSelect = ["id", "displayName", "userPrincipalName", "mail", "accountEnabled"];

    private readonly GraphServiceClient _graph;
    private readonly GraphOptions _options;
    private readonly ILogger<MicrosoftGraphStaffDirectory> _logger;

    public MicrosoftGraphStaffDirectory(IOptions<GraphOptions> options, ILogger<MicrosoftGraphStaffDirectory> logger)
        : this(CreateGraphClient(options.Value), options, logger)
    {
    }

    internal MicrosoftGraphStaffDirectory(GraphServiceClient graph, IOptions<GraphOptions> options, ILogger<MicrosoftGraphStaffDirectory> logger)
    {
        _graph = graph;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, StaffDirectoryUser>> GetUsersByExternalIdsAsync(IReadOnlyCollection<string> externalIds, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, StaffDirectoryUser>(StringComparer.Ordinal);
        foreach (var externalId in externalIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
        {
            var user = await GetGraphUserOrNullAsync(externalId, cancellationToken);
            if (user is null || string.IsNullOrWhiteSpace(user.Id))
            {
                continue;
            }

            var capabilities = await GetCapabilitiesAsync(user.Id, cancellationToken);
            results[user.Id] = ToDirectoryUser(user, capabilities);
        }

        return results;
    }

    public async Task<StaffDirectoryUser> CreateUserAsync(string email, string displayName, string initialPassword, CancellationToken cancellationToken = default)
    {
        var graphUser = new GraphUser
        {
            AccountEnabled = true,
            DisplayName = displayName,
            UserPrincipalName = email,
            MailNickname = CreateMailNickname(email),
            Identities =
            [
                new ObjectIdentity
                {
                    SignInType = "emailAddress",
                    Issuer = _options.TenantId,
                    IssuerAssignedId = email,
                },
            ],
            PasswordPolicies = "DisablePasswordExpiration",
            PasswordProfile = new PasswordProfile
            {
                Password = initialPassword,
                ForceChangePasswordNextSignIn = true,
            },
        };

        var created = await _graph.Users.PostAsync(graphUser, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Microsoft Graph did not return the created user.");
        if (string.IsNullOrWhiteSpace(created.Id))
        {
            throw new InvalidOperationException("Microsoft Graph returned a created user without an object id.");
        }

        LogStaffIdentityCreated(_logger, created.Id, DateTime.UtcNow, null);
        return ToDirectoryUser(created, []);
    }

    public async Task<StaffDirectoryUser> UpdateUserAsync(string externalId, string? displayName, bool? isActive, CancellationToken cancellationToken = default)
    {
        var patch = new GraphUser();
        if (displayName is not null)
        {
            patch.DisplayName = displayName;
        }

        if (isActive.HasValue)
        {
            patch.AccountEnabled = isActive.Value;
        }

        await _graph.Users[externalId].PatchAsync(patch, cancellationToken: cancellationToken);
        var updated = await GetRequiredGraphUserAsync(externalId, cancellationToken);
        var capabilities = await GetCapabilitiesAsync(externalId, cancellationToken);
        return ToDirectoryUser(updated, capabilities);
    }

    public async Task<IReadOnlyList<string>> GetCapabilitiesAsync(string externalId, CancellationToken cancellationToken = default)
    {
        var roleMap = await GetApiAppRoleMapAsync(cancellationToken);
        var assignments = await _graph.Users[externalId].AppRoleAssignments.GetAsync(cancellationToken: cancellationToken);
        if (assignments?.Value is null)
        {
            return [];
        }

        var capabilities = assignments.Value
            .Where(assignment => assignment.AppRoleId.HasValue && roleMap.ContainsKey(assignment.AppRoleId.Value))
            .Select(assignment => roleMap[assignment.AppRoleId!.Value])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return capabilities;
    }

    public async Task AssignCapabilityAsync(string externalId, string capability, CancellationToken cancellationToken = default)
    {
        var (servicePrincipalId, roleId) = await ResolveAppRoleAsync(capability, cancellationToken);
        var assignments = await _graph.Users[externalId].AppRoleAssignments.GetAsync(cancellationToken: cancellationToken);
        if (assignments?.Value?.Any(assignment => assignment.AppRoleId == roleId && assignment.ResourceId == servicePrincipalId) == true)
        {
            return;
        }

        await _graph.Users[externalId].AppRoleAssignments.PostAsync(new AppRoleAssignment
        {
            PrincipalId = Guid.Parse(externalId),
            ResourceId = servicePrincipalId,
            AppRoleId = roleId,
        }, cancellationToken: cancellationToken);
    }

    public async Task RemoveCapabilityAsync(string externalId, string capability, CancellationToken cancellationToken = default)
    {
        var (servicePrincipalId, roleId) = await ResolveAppRoleAsync(capability, cancellationToken);
        var assignments = await _graph.Users[externalId].AppRoleAssignments.GetAsync(cancellationToken: cancellationToken);
        var assignment = assignments?.Value?.FirstOrDefault(candidate =>
            candidate.AppRoleId == roleId && candidate.ResourceId == servicePrincipalId);
        if (assignment?.Id is null)
        {
            return;
        }

        await _graph.Users[externalId].AppRoleAssignments[assignment.Id].DeleteAsync(cancellationToken: cancellationToken);
    }

    public async Task ResetPasswordAsync(string externalId, string newPassword, bool forceChangePasswordNextSignIn, CancellationToken cancellationToken = default)
    {
        await _graph.Users[externalId].PatchAsync(new GraphUser
        {
            PasswordProfile = new PasswordProfile
            {
                Password = newPassword,
                ForceChangePasswordNextSignIn = forceChangePasswordNextSignIn,
            },
        }, cancellationToken: cancellationToken);
    }

    private static GraphServiceClient CreateGraphClient(GraphOptions options)
    {
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            TenantId = options.TenantId,
        });
        return new GraphServiceClient(credential, GraphScopes);
    }

    private async Task<GraphUser?> GetGraphUserOrNullAsync(string externalId, CancellationToken cancellationToken)
    {
        try
        {
            return await _graph.Users[externalId].GetAsync(request =>
            {
                request.QueryParameters.Select = UserSelect;
            }, cancellationToken);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError error) when (error.ResponseStatusCode == 404)
        {
            return null;
        }
    }

    private async Task<GraphUser> GetRequiredGraphUserAsync(string externalId, CancellationToken cancellationToken)
    {
        var user = await GetGraphUserOrNullAsync(externalId, cancellationToken);
        return user ?? throw new InvalidOperationException($"Microsoft Graph user '{externalId}' was not found after update.");
    }

    private async Task<(Guid ServicePrincipalId, Guid RoleId)> ResolveAppRoleAsync(string capability, CancellationToken cancellationToken)
    {
        var servicePrincipal = await GetApiServicePrincipalAsync(cancellationToken);
        var role = servicePrincipal.AppRoles?.FirstOrDefault(candidate =>
            candidate.IsEnabled == true && string.Equals(candidate.Value, capability, StringComparison.Ordinal));
        if (role?.Id is null)
        {
            throw new InvalidOperationException($"Microsoft Graph API service principal does not expose app role '{capability}'.");
        }

        return (Guid.Parse(servicePrincipal.Id!), role.Id.Value);
    }

    private async Task<Dictionary<Guid, string>> GetApiAppRoleMapAsync(CancellationToken cancellationToken)
    {
        var servicePrincipal = await GetApiServicePrincipalAsync(cancellationToken);
        return servicePrincipal.AppRoles?
            .Where(role => role is { IsEnabled: true, Id: not null } && ApplicationCapability.All.Contains(role.Value ?? string.Empty))
            .ToDictionary(role => role.Id!.Value, role => role.Value!, EqualityComparer<Guid>.Default)
            ?? [];
    }

    private async Task<ServicePrincipal> GetApiServicePrincipalAsync(CancellationToken cancellationToken)
    {
        var escapedAppId = _options.ApiClientAppId.Replace("'", "''", StringComparison.Ordinal);
        var servicePrincipals = await _graph.ServicePrincipals.GetAsync(request =>
        {
            request.QueryParameters.Filter = $"appId eq '{escapedAppId}'";
            request.QueryParameters.Select = ["id", "appId", "appRoles"];
        }, cancellationToken);

        var servicePrincipal = servicePrincipals?.Value?.SingleOrDefault();
        if (servicePrincipal?.Id is null)
        {
            throw new InvalidOperationException("Microsoft Graph API service principal was not found for configured Graph:ApiClientAppId.");
        }

        return servicePrincipal;
    }

    private static StaffDirectoryUser ToDirectoryUser(GraphUser user, IReadOnlyList<string> capabilities)
    {
        var userName = user.Mail ?? user.UserPrincipalName ?? user.Id ?? string.Empty;
        return new StaffDirectoryUser(
            user.Id ?? string.Empty,
            userName,
            user.DisplayName ?? userName,
            user.AccountEnabled ?? false,
            capabilities);
    }

    private static string CreateMailNickname(string email)
    {
        var localPart = email.Split('@', 2)[0];
        var sanitized = new string(localPart.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "staff" : sanitized;
    }
}
