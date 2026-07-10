namespace AntiguoAserradero.Application.Auth;

public interface ICurrentUser
{
    string? Id { get; }
    string DisplayName { get; }
    IReadOnlyCollection<string> Capabilities { get; }
    bool IsAuthenticated { get; }
}
