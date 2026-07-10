namespace AntiguoAserradero.Domain.Errors;

public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string code, string message, object? details = null)
        : base(code, message, details)
    {
    }
}
