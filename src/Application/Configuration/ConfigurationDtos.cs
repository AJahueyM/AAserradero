namespace AntiguoAserradero.Application.Configuration;

public sealed record ConfigValueDto(string Key, string Value, DateTime UpdatedAt);

public sealed record UpdateConfigValueRequest(string Value);
