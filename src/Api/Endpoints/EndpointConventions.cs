namespace AntiguoAserradero.Api.Endpoints;

/// <summary>
/// Feature agents add minimal API endpoint groups under src\Api\Endpoints\&lt;Feature&gt;.
/// Each feature exposes one static Map&lt;Feature&gt;Endpoints(this IEndpointRouteBuilder) method,
/// maps routes below /api, guards mutations with AuthorizationPolicyNames, and delegates business
/// logic to Application services/handlers instead of embedding rules in endpoint lambdas.
/// Absolute DateTime values must be UTC; the API serializes them as ISO-8601 with trailing Z.
/// </summary>
public static class EndpointConventions;
