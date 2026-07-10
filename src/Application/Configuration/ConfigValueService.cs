using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.Configuration;

public interface IConfigValueService
{
    Task<IReadOnlyList<ConfigValueDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<ConfigValueDto> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<ConfigValueDto> UpdateAsync(string key, UpdateConfigValueRequest request, CancellationToken cancellationToken = default);
}

public sealed class ConfigValueService(IApplicationDbContext dbContext) : IConfigValueService
{
    private static readonly DateTime MissingUpdatedAtUtc = new(1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task<IReadOnlyList<ConfigValueDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var values = await dbContext.ConfigValues
            .AsNoTracking()
            .Where(config => ConfigKeys.Allowed.Contains(config.Key))
            .OrderBy(config => config.Key)
            .ToListAsync(cancellationToken);

        return ConfigKeys.Allowed
            .OrderBy(key => key)
            .Select(key =>
            {
                var config = values.FirstOrDefault(value => value.Key == key);
                return new ConfigValueDto(key, config?.Value ?? string.Empty, config?.UpdatedAt ?? MissingUpdatedAtUtc);
            })
            .ToList();
    }

    public async Task<ConfigValueDto> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        var config = await dbContext.ConfigValues.AsNoTracking().FirstOrDefaultAsync(value => value.Key == key, cancellationToken);
        return new ConfigValueDto(key, config?.Value ?? string.Empty, config?.UpdatedAt ?? MissingUpdatedAtUtc);
    }

    public async Task<ConfigValueDto> UpdateAsync(string key, UpdateConfigValueRequest request, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        if (request.Value.Length > 4000)
        {
            throw new ValidationException("Config.ValueTooLong", "Configuration value cannot exceed 4000 characters.", new { field = nameof(request.Value) });
        }

        var config = await dbContext.ConfigValues.FirstOrDefaultAsync(value => value.Key == key, cancellationToken);
        if (config is null)
        {
            config = new ConfigValue { Key = key, Value = request.Value, UpdatedAt = DateTime.UtcNow };
            dbContext.ConfigValues.Add(config);
        }
        else
        {
            config.Value = request.Value;
            config.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ConfigValueDto(config.Key, config.Value, config.UpdatedAt);
    }

    private static void ValidateKey(string key)
    {
        if (!ConfigKeys.Allowed.Contains(key))
        {
            throw new ValidationException("Config.KeyNotAllowed", "Configuration key is not allowed.", new { key, allowed = ConfigKeys.Allowed });
        }
    }
}
