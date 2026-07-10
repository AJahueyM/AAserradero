using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.Reservations;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.Movements;

public interface IMovementService
{
    Task<IReadOnlyList<MovementResponse>> ListAsync(int reservationId, CancellationToken cancellationToken = default);

    Task<MovementResponse> GetAsync(int reservationId, int movementId, CancellationToken cancellationToken = default);

    Task<ReservationDetailResponse> AddAsync(int reservationId, UpsertMovementRequest request, CancellationToken cancellationToken = default);

    Task<ReservationDetailResponse> UpdateAsync(int reservationId, int movementId, UpsertMovementRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int reservationId, int movementId, CancellationToken cancellationToken = default);

    Task<ReservationDetailResponse> RecomputeReservationAsync(int reservationId, CancellationToken cancellationToken = default);
}

public sealed class MovementService : IMovementService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentStaffResolver _staffResolver;
    private readonly IReservationFinancialService _financialService;
    private readonly IReservationLiveUpdateNotifier _notifier;

    public MovementService(
        IApplicationDbContext dbContext,
        ICurrentStaffResolver staffResolver,
        IReservationFinancialService financialService,
        IReservationLiveUpdateNotifier notifier)
    {
        _dbContext = dbContext;
        _staffResolver = staffResolver;
        _financialService = financialService;
        _notifier = notifier;
    }

    public async Task<IReadOnlyList<MovementResponse>> ListAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        await EnsureReservationExistsAsync(reservationId, cancellationToken);

        var movements = await LoadMovementQuery(_dbContext.Movements.AsNoTracking())
            .Where(movement => movement.ReservationId == reservationId)
            .OrderBy(movement => movement.Date)
            .ThenBy(movement => movement.Id)
            .ToListAsync(cancellationToken);

        return movements.Select(MovementMapper.ToResponse).ToArray();
    }

    public async Task<MovementResponse> GetAsync(int reservationId, int movementId, CancellationToken cancellationToken = default)
    {
        var movement = await LoadMovementQuery(_dbContext.Movements.AsNoTracking())
            .SingleOrDefaultAsync(candidate => candidate.ReservationId == reservationId && candidate.Id == movementId, cancellationToken)
            ?? throw new NotFoundException("Movement.NotFound", $"Movement '{movementId}' was not found.");

        return MovementMapper.ToResponse(movement);
    }

    public async Task<ReservationDetailResponse> AddAsync(int reservationId, UpsertMovementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reservation = await LoadReservationForMutationAsync(reservationId, cancellationToken);
        var concept = await GetActiveConceptAsync(request.ConceptCode, cancellationToken);
        ValidateAmounts(concept, request.Charge, request.Payment);
        var paymentMethod = await GetOptionalPaymentMethodAsync(request.PaymentMethodCode, cancellationToken);
        var paymentLocation = await GetOptionalPaymentLocationAsync(request.PaymentLocationCode, cancellationToken);
        var responsibleUser = await _staffResolver.GetOrCreateAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var movement = new Movement
        {
            ReservationId = reservation.Id,
            Reservation = reservation,
            ConceptId = concept.Id,
            Concept = concept,
            PaymentMethodId = paymentMethod?.Id,
            PaymentMethod = paymentMethod,
            PaymentLocationId = paymentLocation?.Id,
            PaymentLocation = paymentLocation,
            Charge = request.Charge,
            Payment = request.Payment,
            Date = request.Date.HasValue ? ToUtcInstant(request.Date.Value) : now,
            ResponsibleUserId = responsibleUser.Id,
            ResponsibleUser = responsibleUser,
            CreatedAt = now,
        };

        reservation.Movements.Add(movement);
        await RecomputePaymentStatusAsync(reservation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _notifier.PublishMovementChangedAsync(reservation, movement.Id, "created", cancellationToken);

        return await GetReservationDetailAsync(reservation.Id, cancellationToken);
    }

    public async Task<ReservationDetailResponse> UpdateAsync(int reservationId, int movementId, UpsertMovementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reservation = await LoadReservationForMutationAsync(reservationId, cancellationToken);
        var movement = reservation.Movements.SingleOrDefault(candidate => candidate.Id == movementId)
            ?? throw new NotFoundException("Movement.NotFound", $"Movement '{movementId}' was not found.");
        var concept = await GetActiveConceptAsync(request.ConceptCode, cancellationToken);
        ValidateAmounts(concept, request.Charge, request.Payment);
        var paymentMethod = await GetOptionalPaymentMethodAsync(request.PaymentMethodCode, cancellationToken);
        var paymentLocation = await GetOptionalPaymentLocationAsync(request.PaymentLocationCode, cancellationToken);
        var responsibleUser = await _staffResolver.GetOrCreateAsync(cancellationToken);

        movement.ConceptId = concept.Id;
        movement.Concept = concept;
        movement.PaymentMethodId = paymentMethod?.Id;
        movement.PaymentMethod = paymentMethod;
        movement.PaymentLocationId = paymentLocation?.Id;
        movement.PaymentLocation = paymentLocation;
        movement.Charge = request.Charge;
        movement.Payment = request.Payment;
        if (request.Date.HasValue)
        {
            movement.Date = ToUtcInstant(request.Date.Value);
        }

        movement.ResponsibleUserId = responsibleUser.Id;
        movement.ResponsibleUser = responsibleUser;

        await RecomputePaymentStatusAsync(reservation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _notifier.PublishMovementChangedAsync(reservation, movement.Id, "updated", cancellationToken);

        return await GetReservationDetailAsync(reservation.Id, cancellationToken);
    }

    public async Task DeleteAsync(int reservationId, int movementId, CancellationToken cancellationToken = default)
    {
        var reservation = await LoadReservationForMutationAsync(reservationId, cancellationToken);
        var movement = reservation.Movements.SingleOrDefault(candidate => candidate.Id == movementId)
            ?? throw new NotFoundException("Movement.NotFound", $"Movement '{movementId}' was not found.");

        reservation.Movements.Remove(movement);
        _dbContext.Movements.Remove(movement);
        await RecomputePaymentStatusAsync(reservation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _notifier.PublishMovementChangedAsync(reservation, movementId, "deleted", cancellationToken);
    }

    public async Task<ReservationDetailResponse> RecomputeReservationAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        var reservation = await LoadReservationForMutationAsync(reservationId, cancellationToken);
        await RecomputePaymentStatusAsync(reservation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _notifier.PublishReservationChangedAsync(reservation, "recomputed", cancellationToken);

        return await GetReservationDetailAsync(reservation.Id, cancellationToken);
    }

    private static IQueryable<Movement> LoadMovementQuery(IQueryable<Movement> query)
    {
        return query
            .Include(movement => movement.Concept)
            .Include(movement => movement.PaymentMethod)
            .Include(movement => movement.PaymentLocation);
    }

    private static DateTime ToUtcInstant(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    private static void ValidateAmounts(Concept concept, decimal charge, decimal payment)
    {
        ArgumentNullException.ThrowIfNull(concept);

        if (charge < 0m || payment < 0m)
        {
            throw new ValidationException("Movement.AmountInvalid", "Charge and payment must be non-negative.");
        }

        if (charge == 0m && payment == 0m)
        {
            throw new ValidationException("Movement.AmountRequired", "A movement must have a charge or payment amount.");
        }

        if (concept.IsDiscount && (charge != 0m || payment <= 0m))
        {
            throw new ValidationException("Movement.DiscountInvalid", "Discount movements must store the discount amount in payment and have zero charge.");
        }
    }

    private async Task RecomputePaymentStatusAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var financialSummary = _financialService.Calculate(reservation.Movements);
        await _financialService.ApplyPaymentStatusAsync(reservation, financialSummary, cancellationToken);
    }

    private async Task EnsureReservationExistsAsync(int reservationId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Reservations.AnyAsync(reservation => reservation.Id == reservationId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Reservation.NotFound", $"Reservation '{reservationId}' was not found.");
        }
    }

    private async Task<Reservation> LoadReservationForMutationAsync(int reservationId, CancellationToken cancellationToken)
    {
        return await _dbContext.Reservations
            .Include(reservation => reservation.Client)
            .Include(reservation => reservation.Room)
            .Include(reservation => reservation.Status)
            .Include(reservation => reservation.Movements)
                .ThenInclude(movement => movement.Concept)
            .Include(reservation => reservation.Movements)
                .ThenInclude(movement => movement.PaymentMethod)
            .Include(reservation => reservation.Movements)
                .ThenInclude(movement => movement.PaymentLocation)
            .SingleOrDefaultAsync(reservation => reservation.Id == reservationId, cancellationToken)
            ?? throw new NotFoundException("Reservation.NotFound", $"Reservation '{reservationId}' was not found.");
    }

    private async Task<ReservationDetailResponse> GetReservationDetailAsync(int reservationId, CancellationToken cancellationToken)
    {
        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .Include(candidate => candidate.Client)
            .Include(candidate => candidate.Room)
            .Include(candidate => candidate.Status)
            .Include(candidate => candidate.Movements)
                .ThenInclude(movement => movement.Concept)
            .Include(candidate => candidate.Movements)
                .ThenInclude(movement => movement.PaymentMethod)
            .Include(candidate => candidate.Movements)
                .ThenInclude(movement => movement.PaymentLocation)
            .SingleAsync(candidate => candidate.Id == reservationId, cancellationToken);

        return ReservationMapper.ToDetailResponse(reservation, _financialService.Calculate(reservation.Movements));
    }

    private async Task<Concept> GetActiveConceptAsync(string conceptCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conceptCode))
        {
            throw new ValidationException("Movement.ConceptRequired", "A concept code is required.");
        }

        var normalizedCode = conceptCode.Trim();
        return await _dbContext.Concepts.SingleOrDefaultAsync(concept => concept.Code == normalizedCode && concept.IsActive, cancellationToken)
            ?? throw new NotFoundException("Concept.NotFound", $"Concept '{normalizedCode}' was not found.");
    }

    private async Task<PaymentMethod?> GetOptionalPaymentMethodAsync(string? paymentMethodCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(paymentMethodCode))
        {
            return null;
        }

        var normalizedCode = paymentMethodCode.Trim();
        return await _dbContext.PaymentMethods.SingleOrDefaultAsync(method => method.Code == normalizedCode && method.IsActive, cancellationToken)
            ?? throw new NotFoundException("PaymentMethod.NotFound", $"Payment method '{normalizedCode}' was not found.");
    }

    private async Task<PaymentLocation?> GetOptionalPaymentLocationAsync(string? paymentLocationCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(paymentLocationCode))
        {
            return null;
        }

        var normalizedCode = paymentLocationCode.Trim();
        return await _dbContext.PaymentLocations.SingleOrDefaultAsync(location => location.Code == normalizedCode && location.IsActive, cancellationToken)
            ?? throw new NotFoundException("PaymentLocation.NotFound", $"Payment location '{normalizedCode}' was not found.");
    }
}
