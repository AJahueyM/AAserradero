using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.Reference;

public interface IReferenceDataService
{
    Task<PagedResult<ReferenceItemDto>> ListPaymentMethodsAsync(CatalogListQuery query, CancellationToken cancellationToken = default);
    Task<ReferenceItemDto> CreatePaymentMethodAsync(UpsertReferenceItemRequest request, CancellationToken cancellationToken = default);
    Task<ReferenceItemDto> UpdatePaymentMethodAsync(int id, UpsertReferenceItemRequest request, CancellationToken cancellationToken = default);
    Task<ReferenceItemDto> DeactivatePaymentMethodAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<ReferenceItemDto>> ListPaymentLocationsAsync(CatalogListQuery query, CancellationToken cancellationToken = default);
    Task<ReferenceItemDto> CreatePaymentLocationAsync(UpsertReferenceItemRequest request, CancellationToken cancellationToken = default);
    Task<ReferenceItemDto> UpdatePaymentLocationAsync(int id, UpsertReferenceItemRequest request, CancellationToken cancellationToken = default);
    Task<ReferenceItemDto> DeactivatePaymentLocationAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReservationStatusDto>> ListReservationStatusesAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<UserLookupDto>> ListUsersAsync(CatalogListQuery query, CancellationToken cancellationToken = default);
}

public sealed class ReferenceDataService(IApplicationDbContext dbContext) : IReferenceDataService
{
    public Task<PagedResult<ReferenceItemDto>> ListPaymentMethodsAsync(CatalogListQuery query, CancellationToken cancellationToken = default)
    {
        return ListReferenceItemsAsync(dbContext.PaymentMethods.AsNoTracking(), query, ToDto, cancellationToken);
    }

    public async Task<ReferenceItemDto> CreatePaymentMethodAsync(UpsertReferenceItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await CreateReferenceItemAsync(
            request,
            dbContext.PaymentMethods,
            (code, name) => new PaymentMethod { Code = code, Name = name, IsActive = true },
            "PaymentMethod",
            cancellationToken);
        return ToDto(item);
    }

    public async Task<ReferenceItemDto> UpdatePaymentMethodAsync(int id, UpsertReferenceItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await UpdateReferenceItemAsync(id, request, dbContext.PaymentMethods, "PaymentMethod", cancellationToken);
        return ToDto(item);
    }

    public async Task<ReferenceItemDto> DeactivatePaymentMethodAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await DeactivateReferenceItemAsync(id, dbContext.PaymentMethods, "PaymentMethod", cancellationToken);
        return ToDto(item);
    }

    public Task<PagedResult<ReferenceItemDto>> ListPaymentLocationsAsync(CatalogListQuery query, CancellationToken cancellationToken = default)
    {
        return ListReferenceItemsAsync(dbContext.PaymentLocations.AsNoTracking(), query, ToDto, cancellationToken);
    }

    public async Task<ReferenceItemDto> CreatePaymentLocationAsync(UpsertReferenceItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await CreateReferenceItemAsync(
            request,
            dbContext.PaymentLocations,
            (code, name) => new PaymentLocation { Code = code, Name = name, IsActive = true },
            "PaymentLocation",
            cancellationToken);
        return ToDto(item);
    }

    public async Task<ReferenceItemDto> UpdatePaymentLocationAsync(int id, UpsertReferenceItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await UpdateReferenceItemAsync(id, request, dbContext.PaymentLocations, "PaymentLocation", cancellationToken);
        return ToDto(item);
    }

    public async Task<ReferenceItemDto> DeactivatePaymentLocationAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await DeactivateReferenceItemAsync(id, dbContext.PaymentLocations, "PaymentLocation", cancellationToken);
        return ToDto(item);
    }

    public async Task<IReadOnlyList<ReservationStatusDto>> ListReservationStatusesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.ReservationStatuses
            .AsNoTracking()
            .OrderBy(status => status.SortOrder)
            .ThenBy(status => status.Id)
            .Select(status => new ReservationStatusDto(status.Id, status.Code, status.Label, status.SortOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<UserLookupDto>> ListUsersAsync(CatalogListQuery query, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = ReferenceValidation.NormalizePaging(query.Page, query.PageSize);
        var search = query.Search?.Trim();
        var users = dbContext.Users.AsNoTracking().Where(user => user.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            users = users.Where(user => user.DisplayName.Contains(search));
        }

        var total = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new UserLookupDto(user.Id, user.DisplayName))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserLookupDto>(items, page, pageSize, total);
    }

    private async Task<T> CreateReferenceItemAsync<T>(
        UpsertReferenceItemRequest request,
        DbSet<T> set,
        Func<string, string, T> factory,
        string codePrefix,
        CancellationToken cancellationToken)
        where T : class
    {
        var code = ReferenceValidation.NormalizeCode(request.Code, $"{codePrefix}.CodeRequired");
        var name = ReferenceValidation.NormalizeName(request.Name, $"{codePrefix}.NameRequired");
        await EnsureUniqueCodeAsync(set, code, null, codePrefix, cancellationToken);
        var item = factory(code, name);
        set.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return item;
    }

    private async Task<T> UpdateReferenceItemAsync<T>(int id, UpsertReferenceItemRequest request, DbSet<T> set, string codePrefix, CancellationToken cancellationToken)
        where T : class
    {
        var item = await set.FirstOrDefaultAsync(reference => EF.Property<int>(reference, "Id") == id, cancellationToken)
            ?? throw new NotFoundException($"{codePrefix}.NotFound", $"{codePrefix} was not found.");
        var code = ReferenceValidation.NormalizeCode(request.Code, $"{codePrefix}.CodeRequired");
        var name = ReferenceValidation.NormalizeName(request.Name, $"{codePrefix}.NameRequired");
        await EnsureUniqueCodeAsync(set, code, id, codePrefix, cancellationToken);
        typeof(T).GetProperty("Code")?.SetValue(item, code);
        typeof(T).GetProperty("Name")?.SetValue(item, name);
        await dbContext.SaveChangesAsync(cancellationToken);
        return item;
    }

    private async Task<T> DeactivateReferenceItemAsync<T>(int id, DbSet<T> set, string codePrefix, CancellationToken cancellationToken)
        where T : class
    {
        var item = await set.FirstOrDefaultAsync(reference => EF.Property<int>(reference, "Id") == id, cancellationToken)
            ?? throw new NotFoundException($"{codePrefix}.NotFound", $"{codePrefix} was not found.");
        typeof(T).GetProperty("IsActive")?.SetValue(item, false);
        await dbContext.SaveChangesAsync(cancellationToken);
        return item;
    }

    private static async Task EnsureUniqueCodeAsync<T>(DbSet<T> set, string code, int? currentId, string codePrefix, CancellationToken cancellationToken)
        where T : class
    {
        var exists = await set.AnyAsync(item => EF.Property<string>(item, "Code") == code && EF.Property<int>(item, "Id") != currentId, cancellationToken);
        if (exists)
        {
            throw new ConflictException($"{codePrefix}.CodeDuplicate", $"{codePrefix} code already exists.");
        }
    }

    private static async Task<PagedResult<ReferenceItemDto>> ListReferenceItemsAsync<T>(
        IQueryable<T> itemsQuery,
        CatalogListQuery query,
        Func<T, ReferenceItemDto> mapper,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = ReferenceValidation.NormalizePaging(query.Page, query.PageSize);
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            itemsQuery = itemsQuery.Where(item => EF.Property<string>(item!, "Name").Contains(search) || EF.Property<string>(item!, "Code").Contains(search));
        }

        var total = await itemsQuery.CountAsync(cancellationToken);
        var items = await itemsQuery
            .OrderBy(item => EF.Property<string>(item!, "Name"))
            .ThenBy(item => EF.Property<int>(item!, "Id"))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ReferenceItemDto>(items.Select(mapper).ToList(), page, pageSize, total);
    }

    private static ReferenceItemDto ToDto(PaymentMethod item)
    {
        return new ReferenceItemDto(item.Id, item.Code, item.Name, item.IsActive);
    }

    private static ReferenceItemDto ToDto(PaymentLocation item)
    {
        return new ReferenceItemDto(item.Id, item.Code, item.Name, item.IsActive);
    }
}
