using AntiguoAserradero.Application.Auth;
using AntiguoAserradero.Application.Users;
using AntiguoAserradero.Domain.Errors;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using GraphUser = Microsoft.Graph.Models.User;
using ValidationException = AntiguoAserradero.Domain.Errors.ValidationException;

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
            MailNickname = CreateMailNickname(email),
            PasswordProfile = new PasswordProfile
            {
                Password = initialPassword,
                ForceChangePasswordNextSignIn = true,
            },
        };

        if (!string.IsNullOrWhiteSpace(_options.LocalAccountIssuer))
        {
            // Entra External ID (CIAM) tenant: create an email-based local account.
            graphUser.Identities =
            [
                new ObjectIdentity
                {
                    SignInType = "emailAddress",
                    Issuer = _options.LocalAccountIssuer,
                    IssuerAssignedId = email,
                },
            ];
            graphUser.PasswordPolicies = "DisablePasswordExpiration";
        }
        else
        {
            // Workforce tenant: create a member account with a UPN in a verified tenant domain.
            var domain = await ResolveUserDomainAsync(cancellationToken);
            graphUser.UserPrincipalName = BuildUserPrincipalName(email, domain);
            if (LooksLikeEmail(email))
            {
                graphUser.OtherMails = [email];
            }
        }

        GraphUser created;
        try
        {
            created = await _graph.Users.PostAsync(graphUser, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Microsoft Graph did not return the created user.");
        }
        catch (ODataError error)
        {
            throw TranslateGraphError(error);
        }

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

        try
        {
            await _graph.Users[externalId].PatchAsync(patch, cancellationToken: cancellationToken);
        }
        catch (ODataError error)
        {
            throw TranslateGraphError(error);
        }

        var updated = await GetRequiredGraphUserAsync(externalId, cancellationToken);
        var capabilities = await GetCapabilitiesAsync(externalId, cancellationToken);
        return ToDirectoryUser(updated, capabilities);
    }

    public async Task<IReadOnlyList<string>> GetCapabilitiesAsync(string externalId, CancellationToken cancellationToken = default)
    {
        var roleMap = await GetApiAppRoleMapAsync(cancellationToken);
        // When listing users, a stale/removed directory object should not fail the whole request.
        var assignments = await GetAppRoleAssignmentsOrEmptyAsync(externalId, cancellationToken);
        if (assignments.Count == 0)
        {
            return [];
        }

        var capabilities = assignments
            .Where(assignment => assignment.AppRoleId.HasValue && roleMap.ContainsKey(assignment.AppRoleId.Value))
            .Select(assignment => roleMap[assignment.AppRoleId!.Value])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return capabilities;
    }

    public async Task AssignCapabilityAsync(string externalId, string capability, CancellationToken cancellationToken = default)
    {
        try
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
        catch (ODataError error)
        {
            throw TranslateGraphError(error);
        }
    }

    public async Task RemoveCapabilityAsync(string externalId, string capability, CancellationToken cancellationToken = default)
    {
        try
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
        catch (ODataError error)
        {
            throw TranslateGraphError(error);
        }
    }

    public async Task ResetPasswordAsync(string externalId, string newPassword, bool forceChangePasswordNextSignIn, CancellationToken cancellationToken = default)
    {
        try
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
        catch (ODataError error)
        {
            throw TranslateGraphError(error);
        }
    }

    private static GraphServiceClient CreateGraphClient(GraphOptions options)
    {
        TokenCredential credential;
        if (!string.IsNullOrWhiteSpace(options.ProvisioningClientId))
        {
            // Secretless cross-tenant access: the container's managed identity is registered as a
            // federated identity credential on the provisioning app in the target tenant. Exchange
            // the managed-identity token (audience api://AzureADTokenExchange) for a Graph token as
            // that app — no client secret or certificate involved.
            var managedIdentityCredential = new ManagedIdentityCredential();
            credential = new ClientAssertionCredential(
                options.TenantId,
                options.ProvisioningClientId,
                async cancellationToken =>
                {
                    var token = await managedIdentityCredential.GetTokenAsync(
                        new TokenRequestContext(["api://AzureADTokenExchange/.default"]),
                        cancellationToken);
                    return token.Token;
                });
        }
        else
        {
            credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                TenantId = options.TenantId,
            });
        }

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
        return user ?? throw new NotFoundException("StaffDirectory.UserNotFound", $"The staff user '{externalId}' was not found in the directory.");
    }

    private async Task<IReadOnlyList<AppRoleAssignment>> GetAppRoleAssignmentsOrEmptyAsync(string externalId, CancellationToken cancellationToken)
    {
        try
        {
            var assignments = await _graph.Users[externalId].AppRoleAssignments.GetAsync(cancellationToken: cancellationToken);
            return assignments?.Value ?? [];
        }
        catch (ODataError error) when (error.ResponseStatusCode == 404)
        {
            return [];
        }
        catch (ODataError error)
        {
            throw TranslateGraphError(error);
        }
    }

    // Translate Microsoft Graph errors into typed domain errors so the API returns consistent,
    // meaningful status codes. Unexpected (e.g. 5xx) errors are rethrown as-is so they surface as
    // genuine 500s and are captured by Application Insights.
    private static Exception TranslateGraphError(ODataError error) => error.ResponseStatusCode switch
    {
        400 => new ValidationException("StaffDirectory.InvalidRequest", string.IsNullOrWhiteSpace(error.Message) ? "The directory request was invalid." : error.Message),
        403 => new ForbiddenException("StaffDirectory.Forbidden", "The service is not permitted to perform this directory operation. Ensure the managed identity has admin-consented Microsoft Graph permissions (User.ReadWrite.All, AppRoleAssignment.ReadWrite.All, Directory.Read.All)."),
        404 => new NotFoundException("StaffDirectory.NotFound", "The requested directory resource was not found."),
        409 => new ConflictException("StaffDirectory.Conflict", "The directory operation conflicts with the current state of the resource."),
        _ => error,
    };

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

    private static bool LooksLikeEmail(string value) =>
        value.Contains('@', StringComparison.Ordinal) && value.Split('@', 2)[1].Contains('.', StringComparison.Ordinal);

    // Builds a member-account UPN in a verified tenant domain, e.g. "jdoe@contoso.onmicrosoft.com".
    // The user-provided value may be a bare username or an email; only its local part is used.
    private static string BuildUserPrincipalName(string emailOrUsername, string userDomain)
    {
        var localPart = emailOrUsername.Contains('@', StringComparison.Ordinal)
            ? emailOrUsername.Split('@', 2)[0]
            : emailOrUsername;
        var sanitized = new string(localPart.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_').ToArray()).Trim('.');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "staff";
        }

        return $"{sanitized}@{userDomain.TrimStart('@')}";
    }

    // Resolves the verified domain to use for new member-account UPNs: the configured Graph:UserDomain,
    // otherwise the tenant's default verified domain.
    private async Task<string> ResolveUserDomainAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.UserDomain))
        {
            return _options.UserDomain.TrimStart('@');
        }

        var organization = await _graph.Organization.GetAsync(request =>
        {
            request.QueryParameters.Select = ["verifiedDomains"];
        }, cancellationToken);

        var domains = organization?.Value?.FirstOrDefault()?.VerifiedDomains;
        var domain = domains?.FirstOrDefault(candidate => candidate.IsDefault == true) ?? domains?.FirstOrDefault();
        return domain?.Name
            ?? throw new InvalidOperationException("No verified domain is available for member-account creation. Set Graph:UserDomain.");
    }
}
