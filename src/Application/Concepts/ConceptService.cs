using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.Reference;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.Concepts;

public interface IConceptService
{
    Task<PagedResult<ConceptDto>> ListAsync(CatalogListQuery query, CancellationToken cancellationToken = default);
    Task<ConceptDto> CreateAsync(UpsertConceptRequest request, CancellationToken cancellationToken = default);
    Task<ConceptDto> UpdateAsync(int id, UpsertConceptRequest request, CancellationToken cancellationToken = default);
    Task<ConceptDto> DeactivateAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class ConceptService(IApplicationDbContext dbContext) : IConceptService
{
    public async Task<PagedResult<ConceptDto>> ListAsync(CatalogListQuery query, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = ReferenceValidation.NormalizePaging(query.Page, query.PageSize);
        var search = query.Search?.Trim();
        var concepts = dbContext.Concepts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            concepts = concepts.Where(concept => concept.Name.Contains(search) || concept.Code.Contains(search));
        }

        var total = await concepts.CountAsync(cancellationToken);
        var items = await concepts
            .OrderBy(concept => concept.Name)
            .ThenBy(concept => concept.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(concept => new ConceptDto(concept.Id, concept.Code, concept.Name, concept.IsDiscount, concept.IsProtected, concept.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<ConceptDto>(items, page, pageSize, total);
    }

    public async Task<ConceptDto> CreateAsync(UpsertConceptRequest request, CancellationToken cancellationToken = default)
    {
        var code = ReferenceValidation.NormalizeCode(request.Code, "Concept.CodeRequired");
        var name = ReferenceValidation.NormalizeName(request.Name, "Concept.NameRequired");
        await EnsureUniqueCodeAsync(code, null, cancellationToken);
        var concept = new Concept { Code = code, Name = name, IsDiscount = request.IsDiscount, IsProtected = false, IsActive = true };
        dbContext.Concepts.Add(concept);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(concept);
    }

    public async Task<ConceptDto> UpdateAsync(int id, UpsertConceptRequest request, CancellationToken cancellationToken = default)
    {
        var concept = await FindAsync(id, cancellationToken);
        var code = ReferenceValidation.NormalizeCode(request.Code, "Concept.CodeRequired");
        var name = ReferenceValidation.NormalizeName(request.Name, "Concept.NameRequired");
        await EnsureUniqueCodeAsync(code, id, cancellationToken);
        concept.Code = code;
        concept.Name = name;
        concept.IsDiscount = request.IsDiscount;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(concept);
    }

    public async Task<ConceptDto> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var concept = await FindAsync(id, cancellationToken);
        if (concept.IsProtected)
        {
            throw new ConflictException("Concept.Protected", "Protected concepts cannot be deactivated.");
        }

        concept.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(concept);
    }

    private async Task<Concept> FindAsync(int id, CancellationToken cancellationToken)
    {
        return await dbContext.Concepts.FirstOrDefaultAsync(concept => concept.Id == id, cancellationToken)
            ?? throw new NotFoundException("Concept.NotFound", "Concept was not found.");
    }

    private async Task EnsureUniqueCodeAsync(string code, int? currentId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Concepts.AnyAsync(concept => concept.Code == code && concept.Id != currentId, cancellationToken);
        if (exists)
        {
            throw new ConflictException("Concept.CodeDuplicate", "A concept with this code already exists.");
        }
    }

    private static ConceptDto ToDto(Concept concept)
    {
        return new ConceptDto(concept.Id, concept.Code, concept.Name, concept.IsDiscount, concept.IsProtected, concept.IsActive);
    }
}
