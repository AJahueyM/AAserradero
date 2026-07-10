namespace AntiguoAserradero.Domain.Errors;

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string code, string message, object? details = null)
        : base(code, message, details)
    {
    }
}
