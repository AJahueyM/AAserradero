namespace AntiguoAserradero.Application.Reference;

public sealed record ReferenceItemDto(int Id, string Code, string Name, bool IsActive);

public sealed record UpsertReferenceItemRequest(string Code, string Name);

public sealed record ReservationStatusDto(int Id, string Code, string Label, int SortOrder);

public sealed record UserLookupDto(int Id, string DisplayName);
