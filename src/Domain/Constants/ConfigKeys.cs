namespace AntiguoAserradero.Domain.Constants;

public static class ConfigKeys
{
    public const string PaymentInstructions = "PaymentInstructions";

    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        PaymentInstructions,
    };
}
