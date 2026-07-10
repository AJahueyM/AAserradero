using AntiguoAserradero.Domain.Errors;

namespace AntiguoAserradero.Application.Reference;

internal static class ReferenceValidation
{
    public static string NormalizeCode(string code, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ValidationException(errorCode, "Code is required.", new { field = "Code" });
        }

        return code.Trim().ToUpperInvariant();
    }

    public static string NormalizeName(string name, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(errorCode, "Name is required.", new { field = "Name" });
        }

        return name.Trim();
    }

    public static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        return (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
    }
}
