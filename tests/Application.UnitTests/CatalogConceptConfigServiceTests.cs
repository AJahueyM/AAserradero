using AntiguoAserradero.Application.Concepts;
using AntiguoAserradero.Application.Configuration;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using AntiguoAserradero.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.UnitTests;

public sealed class CatalogConceptConfigServiceTests
{
    [Fact]
    public async Task ProtectedConceptCannotBeDeactivated()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Concepts.Add(new Concept { Id = 1, Code = "LODGING", Name = "Hospedaje", IsProtected = true });
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ConflictException>(() => new ConceptService(dbContext).DeactivateAsync(1));

        Assert.Equal("Concept.Protected", exception.Code);
    }

    [Fact]
    public async Task ConfigUpdateRejectsUnknownKeys()
    {
        await using var dbContext = CreateDbContext();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            new ConfigValueService(dbContext).UpdateAsync("Unknown", new UpdateConfigValueRequest("x")));

        Assert.Equal("Config.KeyNotAllowed", exception.Code);
    }

    [Fact]
    public async Task ConfigUpdateCreatesAllowedValueWhenMissing()
    {
        await using var dbContext = CreateDbContext();

        var result = await new ConfigValueService(dbContext).UpdateAsync("PaymentInstructions", new UpdateConfigValueRequest("Pagar en recepción."));

        Assert.Equal("PaymentInstructions", result.Key);
        Assert.Equal("Pagar en recepción.", result.Value);
        Assert.Equal(DateTimeKind.Utc, result.UpdatedAt.Kind);
    }

    private static AntiguoAserraderoDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AntiguoAserraderoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AntiguoAserraderoDbContext(options);
    }
}
