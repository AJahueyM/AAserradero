namespace AntiguoAserradero.Application.Concepts;

public sealed record ConceptDto(int Id, string Code, string Name, bool IsDiscount, bool IsProtected, bool IsActive);

public sealed record UpsertConceptRequest(string Code, string Name, bool IsDiscount);
