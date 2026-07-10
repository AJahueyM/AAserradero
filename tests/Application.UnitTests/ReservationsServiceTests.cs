using AntiguoAserradero.Application.Reservations;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Errors;

namespace AntiguoAserradero.Application.UnitTests;

public sealed class ReservationsServiceTests
{
    [Fact]
    public async Task CreateAddsInitialLodgingChargeAndDerivesPendingStatusAtomically()
    {
        using var context = BookingCoreTestContext.Create();
        var request = context.NewCreateRequest(fare: 2000m);

        var reservation = await context.ReservationService.CreateAsync(request);

        Assert.Equal(1, context.AppDbContext.SaveChangesAsyncCallCount);
        Assert.Equal(4000m, reservation.FinancialSummary.Charges);
        Assert.Equal(0m, reservation.FinancialSummary.Payments);
        Assert.Equal(4000m, reservation.FinancialSummary.OutstandingBalance);
        Assert.Equal(ReservationStatusCodes.Pending, reservation.Status.Code);
        var movement = Assert.Single(reservation.Movements);
        Assert.Equal(BillingSeedCodes.LodgingConcept, movement.Concept.Code);
        Assert.Equal(4000m, movement.Charge);
        Assert.Equal(0m, movement.Payment);
        Assert.Equal(context.StaffUser.Id, reservation.CreatedById);
        Assert.Contains(context.Notifier.Events, candidate => candidate.StartsWith("reservation:created:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateFailureBeforeSaveDoesNotPersistReservationOrMovement()
    {
        using var context = BookingCoreTestContext.Create();
        var lodging = context.DbContext.Concepts.Single(concept => concept.Code == BillingSeedCodes.LodgingConcept);
        lodging.IsActive = false;
        context.DbContext.SaveChanges();
        context.AppDbContext.ResetSaveCount();

        await Assert.ThrowsAsync<NotFoundException>(() => context.ReservationService.CreateAsync(context.NewCreateRequest()));

        Assert.Equal(0, context.AppDbContext.SaveChangesAsyncCallCount);
        Assert.Empty(context.DbContext.Reservations);
        Assert.Empty(context.DbContext.Movements);
    }

    [Fact]
    public async Task CreateRejectsConflictingReservationButAllowsAdjacentStay()
    {
        using var context = BookingCoreTestContext.Create();
        _ = await context.ReservationService.CreateAsync(context.NewCreateRequest());

        var conflict = context.NewCreateRequest(
            entryDate: new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc),
            exitDate: new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc));
        var exception = await Assert.ThrowsAsync<ConflictException>(() => context.ReservationService.CreateAsync(conflict));
        Assert.Equal("Reservation.Conflict", exception.Code);

        var adjacent = context.NewCreateRequest(
            entryDate: new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc),
            exitDate: new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc));
        var created = await context.ReservationService.CreateAsync(adjacent);
        Assert.Equal(new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc), created.EntryDate);
    }

    [Fact]
    public async Task UpdateAvailabilityExcludesReservationItself()
    {
        using var context = BookingCoreTestContext.Create();
        var reservation = await context.ReservationService.CreateAsync(context.NewCreateRequest());

        var updated = await context.ReservationService.UpdateAsync(reservation.Id, BookingCoreTestContext.NewUpdateRequest(reservation));

        Assert.Equal(reservation.Id, updated.Id);
        Assert.Equal(reservation.EntryDate, updated.EntryDate);
    }

    [Fact]
    public async Task UpdateRejectsCapacityExceeded()
    {
        using var context = BookingCoreTestContext.Create();
        var reservation = await context.ReservationService.CreateAsync(context.NewCreateRequest());
        var tooManyOccupants = new UpdateReservationRequest(
            reservation.Room.Id,
            reservation.Client.Id,
            reservation.EntryDate,
            reservation.ExitDate,
            4,
            1,
            0,
            0,
            reservation.Fare,
            reservation.Status.Code,
            reservation.PromotorId,
            reservation.Notes);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => context.ReservationService.UpdateAsync(reservation.Id, tooManyOccupants));
        Assert.Equal("Reservation.CapacityExceeded", exception.Code);
    }

    [Fact]
    public async Task CancelSetsCancelledStatusAndExcludesFutureConflicts()
    {
        using var context = BookingCoreTestContext.Create();
        var reservation = await context.ReservationService.CreateAsync(context.NewCreateRequest());

        await context.ReservationService.CancelAsync(reservation.Id);
        var cancelled = await context.ReservationService.GetAsync(reservation.Id);
        var replacement = await context.ReservationService.CreateAsync(context.NewCreateRequest());

        Assert.Equal(ReservationStatusCodes.Cancelled, cancelled.Status.Code);
        Assert.NotEqual(reservation.Id, replacement.Id);
    }
    [Fact]
    public async Task SearchSupportsDateModesAndClientFilters()
    {
        using var context = BookingCoreTestContext.Create();
        var reservation = await context.ReservationService.CreateAsync(context.NewCreateRequest());

        var calendar = await context.ReservationService.SearchAsync(new ReservationSearchRequest(
            new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc),
            ReservationDateSearchMode.Calendar,
            null,
            null,
            null));
        var arrivals = await context.ReservationService.SearchAsync(new ReservationSearchRequest(
            reservation.EntryDate,
            reservation.EntryDate,
            ReservationDateSearchMode.Arrivals,
            null,
            null,
            null));
        var departures = await context.ReservationService.SearchAsync(new ReservationSearchRequest(
            reservation.ExitDate,
            reservation.ExitDate,
            ReservationDateSearchMode.Departures,
            null,
            null,
            null));
        var byClient = await context.ReservationService.SearchAsync(new ReservationSearchRequest(null, null, ReservationDateSearchMode.Calendar, "555", "Cliente", reservation.Client.Id));

        Assert.Contains(calendar, candidate => candidate.Id == reservation.Id);
        Assert.Contains(arrivals, candidate => candidate.Id == reservation.Id);
        Assert.Contains(departures, candidate => candidate.Id == reservation.Id);
        Assert.Contains(byClient, candidate => candidate.Id == reservation.Id);
    }
}

