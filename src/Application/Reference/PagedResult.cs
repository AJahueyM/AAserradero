namespace AntiguoAserradero.Application.Reference;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record CatalogListQuery(string? Search = null, int Page = 1, int PageSize = 50);
