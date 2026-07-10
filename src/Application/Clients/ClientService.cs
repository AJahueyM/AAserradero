using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.Clients;

public sealed partial class ClientService : IClientService
{
    private const int MaximumPageSize = 100;
    private static readonly CultureInfo MexicanSpanish = CultureInfo.GetCultureInfo("es-MX");

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentStaffResolver _staffResolver;

    public ClientService(IApplicationDbContext dbContext, ICurrentStaffResolver staffResolver)
    {
        _dbContext = dbContext;
        _staffResolver = staffResolver;
    }

    public async Task<ClientListResponse> SearchAsync(string? name, bool? isVip, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        ValidatePagination(page, pageSize);

        var normalizedNameFilter = NormalizeSearchTerm(name);
        var query = _dbContext.Clients
            .AsNoTracking()
            .Where(client => client.IsActive);

        if (normalizedNameFilter is not null)
        {
#pragma warning disable CA1304, CA1311, CA1862 // Keep EF-translatable case-insensitive search; SQL Server collation and normalized casing handle comparisons.
            query = query.Where(client => client.Name.ToUpper().Contains(normalizedNameFilter));
#pragma warning restore CA1304, CA1311, CA1862
        }

        if (isVip.HasValue)
        {
            query = query.Where(client => client.IsVip == isVip.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var clients = await query
            .OrderBy(client => client.Name)
            .ThenBy(client => client.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var activityCounts = await GetRecentActivityCountsAsync(clients.Select(client => client.Id), cancellationToken);
        var items = clients.Select(client => ToDto(client, activityCounts.GetValueOrDefault(client.Id))).ToArray();

        return new ClientListResponse(items, total, page, pageSize);
    }

    public async Task<ClientDto> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = await _dbContext.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.IsActive, cancellationToken);

        if (client is null)
        {
            throw new NotFoundException("Client.NotFound", "Client was not found.", new { id });
        }

        var activityCounts = await GetRecentActivityCountsAsync([client.Id], cancellationToken);
        return ToDto(client, activityCounts.GetValueOrDefault(client.Id));
    }

    public async Task<ClientDto> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _staffResolver.GetOrCreateAsync(cancellationToken);

        var values = NormalizeAndValidate(request.Name, request.TaxId, request.Address, request.Email, request.Phone, request.Cellphone, false, false, null);
        var client = new Client
        {
            Name = values.Name,
            TaxId = values.TaxId,
            Address = values.Address,
            Email = values.Email,
            Phone = values.Phone,
            Cellphone = values.Cellphone,
            IsVip = false,
            IsBlacklisted = false,
            BlacklistReason = null,
            IsActive = true,
        };

        _dbContext.Clients.Add(client);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(client, 0);
    }

    public async Task<ClientDto> UpdateAsync(int id, UpdateClientRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _staffResolver.GetOrCreateAsync(cancellationToken);

        var client = await _dbContext.Clients.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.IsActive, cancellationToken);
        if (client is null)
        {
            throw new NotFoundException("Client.NotFound", "Client was not found.", new { id });
        }

        var values = NormalizeAndValidate(request.Name, request.TaxId, request.Address, request.Email, request.Phone, request.Cellphone, true, request.IsBlacklisted, request.BlacklistReason);
        client.Name = values.Name;
        client.TaxId = values.TaxId;
        client.Address = values.Address;
        client.Email = values.Email;
        client.Phone = values.Phone;
        client.Cellphone = values.Cellphone;
        client.IsVip = request.IsVip;
        client.IsBlacklisted = request.IsBlacklisted;
        client.BlacklistReason = request.IsBlacklisted ? values.BlacklistReason : null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var activityCounts = await GetRecentActivityCountsAsync([client.Id], cancellationToken);
        return ToDto(client, activityCounts.GetValueOrDefault(client.Id));
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        await _staffResolver.GetOrCreateAsync(cancellationToken);

        var client = await _dbContext.Clients.FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.IsActive, cancellationToken);
        if (client is null)
        {
            throw new NotFoundException("Client.NotFound", "Client was not found.", new { id });
        }

        client.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<int, int>> GetRecentActivityCountsAsync(IEnumerable<int> clientIds, CancellationToken cancellationToken)
    {
        var ids = clientIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, int>();
        }

        var cancelledStatusId = await _dbContext.ReservationStatuses
            .Where(status => status.Code == ReservationStatusCodes.Cancelled)
            .Select(status => status.Id)
            .SingleAsync(cancellationToken);
        var activityWindowStart = DateTime.UtcNow.AddMonths(-12);

        return await _dbContext.Reservations
            .AsNoTracking()
            .Where(reservation => ids.Contains(reservation.ClientId)
                && reservation.StatusId != cancelledStatusId
                && reservation.EntryDate >= activityWindowStart)
            .GroupBy(reservation => reservation.ClientId)
            .Select(group => new { ClientId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ClientId, item => item.Count, cancellationToken);
    }

    private static ClientDto ToDto(Client client, int recentActivityCount)
    {
        return new ClientDto(
            client.Id,
            client.Name,
            client.TaxId,
            client.Address,
            client.Email,
            client.Phone,
            client.Cellphone,
            client.IsVip,
            client.IsBlacklisted,
            client.BlacklistReason,
            client.IsActive,
            recentActivityCount);
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaximumPageSize}."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("Client.ValidationFailed", "One or more client fields are invalid.", errors);
        }
    }

    private static NormalizedClientValues NormalizeAndValidate(
        string? name,
        string? taxId,
        string? address,
        string? email,
        string? phone,
        string? cellphone,
        bool allowBlacklist,
        bool isBlacklisted,
        string? blacklistReason)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedName = NormalizeName(name);
        var normalizedTaxId = NormalizeOptionalUpper(taxId);
        var normalizedAddress = NormalizeOptionalText(address);
        var normalizedEmail = NormalizeOptionalLower(email);
        var normalizedPhone = NormalizeOptionalPhone(phone);
        var normalizedCellphone = NormalizeOptionalPhone(cellphone);
        var normalizedBlacklistReason = NormalizeOptionalText(blacklistReason);

        if (normalizedName is null)
        {
            errors["name"] = ["Name is required."];
        }

        if (normalizedCellphone is null)
        {
            errors["cellphone"] = ["Cellphone is required."];
        }

        if (normalizedEmail is not null && !IsValidEmail(normalizedEmail))
        {
            errors["email"] = ["Email format is invalid."];
        }

        if (normalizedTaxId is not null && !TaxIdRegex().IsMatch(normalizedTaxId))
        {
            errors["taxId"] = ["Tax ID format is invalid."];
        }

        if (normalizedPhone is not null && !IsValidPhoneDigits(normalizedPhone))
        {
            errors["phone"] = ["Phone format is invalid."];
        }

        if (normalizedCellphone is not null && !IsValidPhoneDigits(normalizedCellphone))
        {
            errors["cellphone"] = ["Cellphone format is invalid."];
        }

        if (allowBlacklist && isBlacklisted && normalizedBlacklistReason is null)
        {
            errors["blacklistReason"] = ["Blacklist reason is required when the client is blacklisted."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("Client.ValidationFailed", "One or more client fields are invalid.", errors);
        }

        return new NormalizedClientValues(
            normalizedName!,
            normalizedTaxId,
            normalizedAddress,
            normalizedEmail,
            normalizedPhone,
            normalizedCellphone!,
            normalizedBlacklistReason);
    }

    private static string? NormalizeSearchTerm(string? value)
    {
        var normalized = CollapseWhitespace(value)?.ToUpper(MexicanSpanish);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeName(string? value)
    {
        var normalized = CollapseWhitespace(value);
        if (normalized is null)
        {
            return null;
        }

        return MexicanSpanish.TextInfo.ToTitleCase(normalized.ToLower(MexicanSpanish));
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return CollapseWhitespace(value);
    }

    private static string? NormalizeOptionalUpper(string? value)
    {
        return CollapseWhitespace(value)?.ToUpper(MexicanSpanish);
    }

    private static string? NormalizeOptionalLower(string? value)
    {
        return CollapseWhitespace(value)?.ToLower(MexicanSpanish);
    }

    private static string? NormalizeOptionalPhone(string? value)
    {
        var normalized = CollapseWhitespace(value);
        if (normalized is null)
        {
            return null;
        }

        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        return digits.Length > 0 ? digits : normalized;
    }

    private static string? CollapseWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValidPhoneDigits(string value)
    {
        return value.Length is >= 7 and <= 15 && value.All(char.IsDigit);
    }

    [GeneratedRegex("^[A-ZÑ&]{3,4}[0-9]{6}[A-Z0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex TaxIdRegex();

    private sealed record NormalizedClientValues(
        string Name,
        string? TaxId,
        string? Address,
        string? Email,
        string? Phone,
        string Cellphone,
        string? BlacklistReason);
}

