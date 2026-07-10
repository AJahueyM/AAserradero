namespace AntiguoAserradero.Api.Endpoints;

/// <summary>
/// Implemented by a feature to map its minimal API endpoints under <c>/api</c>.
/// Implementations MUST have a public parameterless constructor; they are discovered and
/// mapped automatically at startup (see <see cref="EndpointModuleRegistry"/>), so each feature
/// adds its own endpoint module in its own file with no edits to <c>Program.cs</c>.
/// Guard mutations with <see cref="Security.AuthorizationPolicyNames"/> and delegate business
/// logic to Application services. Absolute DateTime values are UTC (serialized ISO-8601 "Z").
/// </summary>
public interface IEndpointModule
{
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
