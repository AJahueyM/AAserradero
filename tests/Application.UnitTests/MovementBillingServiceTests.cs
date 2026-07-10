using AntiguoAserradero.Application.Movements;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.UnitTests;

public sealed class MovementBillingServiceTests
{
    [Fact]
    public async Task AddUpdateAndDeleteMovementRecomputesReservationStatus()
    {
        using var context = BookingCoreTestContext.Create();
        var reservation = await context.ReservationService.CreateAsync(context.NewCreateRequest(fare: 1000m));
        var paymentRequest = new UpsertMovementRequest(
            BillingSeedCodes.LodgingConcept,
            BillingSeedCodes.DefaultPaymentMethod,
            BillingSeedCodes.DefaultPaymentLocation,
            0m,
            500m,
            new DateTime(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc));

        var partiallyPaid = await context.MovementService.AddAsync(reservation.Id, paymentRequest);
        var payment = partiallyPaid.Movements.Single(movement => movement.Payment == 500m);
        var paid = await context.MovementService.UpdateAsync(reservation.Id, payment.Id, paymentRequest with { Payment = 2000m });
        await context.MovementService.DeleteAsync(reservation.Id, payment.Id);
        var pending = await context.ReservationService.GetAsync(reservation.Id);

        Assert.Equal(ReservationStatusCodes.Partial, partiallyPaid.Status.Code);
        Assert.Equal(1500m, partiallyPaid.FinancialSummary.OutstandingBalance);
        Assert.Equal(ReservationStatusCodes.Paid, paid.Status.Code);
        Assert.Equal(0m, paid.FinancialSummary.OutstandingBalance);
        Assert.Equal(ReservationStatusCodes.Pending, pending.Status.Code);
        Assert.Equal(2000m, pending.FinancialSummary.OutstandingBalance);
        Assert.Contains(context.Notifier.Events, candidate => candidate.StartsWith("movement:created:", StringComparison.Ordinal));
        Assert.Contains(context.Notifier.Events, candidate => candidate.StartsWith("movement:updated:", StringComparison.Ordinal));
        Assert.Contains(context.Notifier.Events, candidate => candidate.StartsWith("movement:deleted:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiscountMovementStoresAmountAsPaymentAndReducesBalance()
    {
        using var context = BookingCoreTestContext.Create();
        var reservation = await context.ReservationService.CreateAsync(context.NewCreateRequest(fare: 1000m));
        var discountRequest = new UpsertMovementRequest(
            BillingSeedCodes.DiscountConcept,
            null,
            null,
            0m,
            250m,
            null);

        var updated = await context.MovementService.AddAsync(reservation.Id, discountRequest);
        var discount = updated.Movements.Single(movement => movement.Concept.IsDiscount);

        Assert.Equal(0m, discount.Charge);
        Assert.Equal(250m, discount.Payment);
        Assert.Equal(2000m, updated.FinancialSummary.Charges);
        Assert.Equal(250m, updated.FinancialSummary.Payments);
        Assert.Equal(1750m, updated.FinancialSummary.OutstandingBalance);
        Assert.Equal(ReservationStatusCodes.Partial, updated.Status.Code);
    }

    [Fact]
    public async Task RecomputeFixesPaymentStatusDriftFromMovements()
    {
        using var context = BookingCoreTestContext.Create();
        var reservation = await context.ReservationService.CreateAsync(context.NewCreateRequest(fare: 1000m));
        var trackedReservation = context.DbContext.Reservations.Single(candidate => candidate.Id == reservation.Id);
        trackedReservation.StatusId = context.StatusId(ReservationStatusCodes.Pending);
        trackedReservation.Movements.Add(new Movement
        {
            ReservationId = reservation.Id,
            ConceptId = context.ConceptId(BillingSeedCodes.LodgingConcept),
            PaymentMethodId = context.DbContext.PaymentMethods.Single(method => method.Code == BillingSeedCodes.DefaultPaymentMethod).Id,
            PaymentLocationId = context.DbContext.PaymentLocations.Single(location => location.Code == BillingSeedCodes.DefaultPaymentLocation).Id,
            Charge = 0m,
            Payment = 2000m,
            Date = DateTime.UtcNow,
            ResponsibleUserId = context.StaffUser.Id,
            CreatedAt = DateTime.UtcNow,
        });
        context.DbContext.SaveChanges();

        var recomputed = await context.MovementService.RecomputeReservationAsync(reservation.Id);

        Assert.Equal(ReservationStatusCodes.Paid, recomputed.Status.Code);
        Assert.Equal(0m, recomputed.FinancialSummary.OutstandingBalance);
        Assert.Contains(context.Notifier.Events, candidate => candidate.StartsWith("reservation:recomputed:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiscountMovementRejectsChargeAmount()
    {
        using var context = BookingCoreTestContext.Create();
        var reservation = await context.ReservationService.CreateAsync(context.NewCreateRequest());
        var invalidDiscount = new UpsertMovementRequest(BillingSeedCodes.DiscountConcept, null, null, 1m, 100m, null);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => context.MovementService.AddAsync(reservation.Id, invalidDiscount));

        Assert.Equal("Movement.DiscountInvalid", exception.Code);
    }

    [Fact]
    public async Task MovementValidatesActiveReferencesAndNonNegativeAmounts()
    {
        using var context = BookingCoreTestContext.Create();
        var reservation = await context.ReservationService.CreateAsync(context.NewCreateRequest());
        var inactiveMethod = context.DbContext.PaymentMethods.Single(method => method.Code == BillingSeedCodes.DefaultPaymentMethod);
        inactiveMethod.IsActive = false;
        context.DbContext.SaveChanges();

        await Assert.ThrowsAsync<ValidationException>(() => context.MovementService.AddAsync(
            reservation.Id,
            new UpsertMovementRequest(BillingSeedCodes.LodgingConcept, null, null, -1m, 0m, null)));
        await Assert.ThrowsAsync<NotFoundException>(() => context.MovementService.AddAsync(
            reservation.Id,
            new UpsertMovementRequest(BillingSeedCodes.LodgingConcept, BillingSeedCodes.DefaultPaymentMethod, null, 0m, 1m, null)));
    }
}
