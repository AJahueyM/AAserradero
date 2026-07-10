using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.Reservations;

public interface IReservationFinancialService
{
    ReservationFinancialSummary Calculate(IEnumerable<Movement> movements);

    Task<ReservationFinancialSummary> CalculateAsync(int reservationId, CancellationToken cancellationToken = default);

    Task ApplyPaymentStatusAsync(Reservation reservation, ReservationFinancialSummary financialSummary, CancellationToken cancellationToken = default);
}

public sealed class ReservationFinancialService : IReservationFinancialService
{
    private readonly IApplicationDbContext _dbContext;

    public ReservationFinancialService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public ReservationFinancialSummary Calculate(IEnumerable<Movement> movements)
    {
        return ReservationMapper.ToFinancialSummary(movements);
    }

    public async Task<ReservationFinancialSummary> CalculateAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        var movements = await _dbContext.Movements
            .AsNoTracking()
            .Where(movement => movement.ReservationId == reservationId)
            .ToListAsync(cancellationToken);

        return Calculate(movements);
    }

    public async Task ApplyPaymentStatusAsync(Reservation reservation, ReservationFinancialSummary financialSummary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(financialSummary);

        var currentStatusCode = reservation.Status?.Code ?? await _dbContext.ReservationStatuses
            .Where(status => status.Id == reservation.StatusId)
            .Select(status => status.Code)
            .SingleOrDefaultAsync(cancellationToken);

        if (currentStatusCode is not null && ReservationPaymentStatusRules.IsProtectedFromPaymentStatus(currentStatusCode))
        {
            return;
        }

        var derivedStatusCode = ReservationPaymentStatusRules.DeriveStatusCode(financialSummary.Charges, financialSummary.Payments);
        var derivedStatus = await _dbContext.ReservationStatuses
            .SingleOrDefaultAsync(status => status.Code == derivedStatusCode, cancellationToken)
            ?? throw new NotFoundException("ReservationStatus.NotFound", $"Reservation status '{derivedStatusCode}' was not found.");

        reservation.StatusId = derivedStatus.Id;
        reservation.Status = derivedStatus;
    }
}
