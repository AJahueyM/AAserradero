namespace AntiguoAserradero.Domain.Errors;

public sealed class ConflictException : DomainException
{
    public ConflictException(string code, string message, object? details = null)
        : base(code, message, details)
    {
    }
}
