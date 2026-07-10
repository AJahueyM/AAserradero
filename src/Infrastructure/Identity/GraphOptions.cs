using System.ComponentModel.DataAnnotations;

namespace AntiguoAserradero.Infrastructure.Identity;

public sealed class GraphOptions
{
    public const string SectionName = "Graph";

    [Required]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    public string ApiClientAppId { get; set; } = string.Empty;

    /// <summary>
    /// Client ID of the provisioning app registration in the target (CIAM) tenant that has a
    /// federated identity credential trusting the container's managed identity. When set, Graph is
    /// accessed secretlessly via a managed-identity client assertion (no client secret). When
    /// empty (e.g. local dev or same-tenant), DefaultAzureCredential is used.
    /// </summary>
    public string ProvisioningClientId { get; set; } = string.Empty;

    /// <summary>
    /// Issuer used for created local-account (email) identities. For Entra External ID this is the
    /// tenant's domain, e.g. <c>contoso.onmicrosoft.com</c>. Falls back to <see cref="TenantId"/>.
    /// </summary>
    public string LocalAccountIssuer { get; set; } = string.Empty;
}
