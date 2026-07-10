using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.Reservations;

public interface IReservationService
{
    Task<IReadOnlyList<ReservationSummaryResponse>> SearchAsync(ReservationSearchRequest request, CancellationToken cancellationToken = default);

    Task<ReservationDetailResponse> GetAsync(int reservationId, CancellationToken cancellationToken = default);

    Task<ReservationDetailResponse> CreateAsync(CreateReservationRequest request, CancellationToken cancellationToken = default);

    Task<ReservationDetailResponse> UpdateAsync(int reservationId, UpdateReservationRequest request, CancellationToken cancellationToken = default);

    Task CancelAsync(int reservationId, CancellationToken cancellationToken = default);
}

public sealed class ReservationService : IReservationService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentStaffResolver _staffResolver;
    private readonly IReservationFinancialService _financialService;
    private readonly IReservationLiveUpdateNotifier _notifier;

    public ReservationService(
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

    public async Task<IReadOnlyList<ReservationSummaryResponse>> SearchAsync(ReservationSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = _dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.Client)
            .Include(reservation => reservation.Room)
            .Include(reservation => reservation.Status)
            .Where(reservation => reservation.IsActive);

        query = ApplyDateFilters(query, request);

        if (request.ClientId.HasValue)
        {
            query = query.Where(reservation => reservation.ClientId == request.ClientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ClientPhone))
        {
            var phone = EscapeLikePattern(request.ClientPhone.Trim());
            query = query.Where(reservation => reservation.Client != null
                && ((reservation.Client.Phone != null && EF.Functions.Like(reservation.Client.Phone, $"%{phone}%", "\\"))
                    || EF.Functions.Like(reservation.Client.Cellphone, $"%{phone}%", "\\")));
        }

        if (!string.IsNullOrWhiteSpace(request.ClientName))
        {
            var name = EscapeLikePattern(request.ClientName.Trim());
            query = query.Where(reservation => reservation.Client != null
                && EF.Functions.Like(reservation.Client.Name, $"%{name}%", "\\"));
        }

        var reservations = await query
            .OrderBy(reservation => reservation.EntryDate)
            .ThenBy(reservation => reservation.RoomId)
            .ThenBy(reservation => reservation.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

        var responses = new List<ReservationSummaryResponse>(reservations.Count);
        foreach (var reservation in reservations)
        {
            var financialSummary = await _financialService.CalculateAsync(reservation.Id, cancellationToken);
            responses.Add(ReservationMapper.ToSummaryResponse(reservation, financialSummary));
        }

        return responses;
    }

    public async Task<ReservationDetailResponse> GetAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        var reservation = await LoadReservationDetailQuery(_dbContext.Reservations.AsNoTracking())
            .SingleOrDefaultAsync(candidate => candidate.Id == reservationId, cancellationToken)
            ?? throw new NotFoundException("Reservation.NotFound", $"Reservation '{reservationId}' was not found.");

        return ReservationMapper.ToDetailResponse(reservation, _financialService.Calculate(reservation.Movements));
    }

    public async Task<ReservationDetailResponse> CreateAsync(CreateReservationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entryDate = ReservationDateRules.ToUtcDate(request.EntryDate);
        var exitDate = ReservationDateRules.ToUtcDate(request.ExitDate);
        var nights = ReservationDateRules.CountNights(entryDate, exitDate);
        var room = await GetActiveRoomAsync(request.RoomId, cancellationToken);
        var client = await GetActiveClientAsync(request.ClientId, cancellationToken);
        var promotor = await GetActiveUserAsync(request.PromotorId, "Reservation.PromotorNotFound", cancellationToken);
        var requestedStatus = await GetReservationStatusAsync(NormalizeStatusCode(request.StatusCode), cancellationToken);
        var fare = request.Fare ?? room.NightlyFare;
        ValidateReservationValues(request.Adults, request.Children, request.Infants, request.Pets, fare, room.Capacity);

        if (!ReservationPaymentStatusRules.IsCancelled(requestedStatus.Code))
        {
            await EnsureNoConflictAsync(room.Id, entryDate, exitDate, null, cancellationToken);
        }

        var creator = await _staffResolver.GetOrCreateAsync(cancellationToken);
        var lodgingConcept = await GetActiveConceptAsync(BillingSeedCodes.LodgingConcept, cancellationToken);
        var defaultPaymentMethod = await GetActivePaymentMethodAsync(BillingSeedCodes.DefaultPaymentMethod, cancellationToken);
        var defaultPaymentLocation = await GetActivePaymentLocationAsync(BillingSeedCodes.DefaultPaymentLocation, cancellationToken);
        var now = DateTime.UtcNow;
        var reservation = new Reservation
        {
            ClientId = client.Id,
            Client = client,
            RoomId = room.Id,
            Room = room,
            EntryDate = entryDate,
            ExitDate = exitDate,
            CheckInTime = TimeOnly.MinValue,
            CheckOutTime = TimeOnly.MinValue,
            Adults = request.Adults,
            Children = request.Children,
            Infants = request.Infants,
            Pets = request.Pets,
            Fare = fare,
            StatusId = requestedStatus.Id,
            Status = requestedStatus,
            PromotorId = promotor.Id,
            Promotor = promotor,
            Notes = NormalizeNotes(request.Notes),
            CreatedById = creator.Id,
            CreatedBy = creator,
            CreatedAt = now,
            IsActive = true,
        };

        reservation.Movements.Add(new Movement
        {
            Reservation = reservation,
            ConceptId = lodgingConcept.Id,
            Concept = lodgingConcept,
            PaymentMethodId = defaultPaymentMethod.Id,
            PaymentMethod = defaultPaymentMethod,
            PaymentLocationId = defaultPaymentLocation.Id,
            PaymentLocation = defaultPaymentLocation,
            Charge = fare * nights,
            Payment = 0m,
            Date = now,
            ResponsibleUserId = creator.Id,
            ResponsibleUser = creator,
            CreatedAt = now,
        });

        await _financialService.ApplyPaymentStatusAsync(reservation, _financialService.Calculate(reservation.Movements), cancellationToken);
        _dbContext.Reservations.Add(reservation);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _notifier.PublishReservationChangedAsync(reservation, "created", cancellationToken);

        return await GetAsync(reservation.Id, cancellationToken);
    }

    public async Task<ReservationDetailResponse> UpdateAsync(int reservationId, UpdateReservationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reservation = await LoadReservationDetailQuery(_dbContext.Reservations)
            .SingleOrDefaultAsync(candidate => candidate.Id == reservationId, cancellationToken)
            ?? throw new NotFoundException("Reservation.NotFound", $"Reservation '{reservationId}' was not found.");

        var entryDate = ReservationDateRules.ToUtcDate(request.EntryDate);
        var exitDate = ReservationDateRules.ToUtcDate(request.ExitDate);
        _ = ReservationDateRules.CountNights(entryDate, exitDate);
        var room = await GetActiveRoomAsync(request.RoomId, cancellationToken);
        var client = await GetActiveClientAsync(request.ClientId, cancellationToken);
        var promotor = await GetActiveUserAsync(request.PromotorId, "Reservation.PromotorNotFound", cancellationToken);
        var requestedStatus = await GetReservationStatusAsync(NormalizeStatusCode(request.StatusCode), cancellationToken);
        var fare = request.Fare ?? room.NightlyFare;
        ValidateReservationValues(request.Adults, request.Children, request.Infants, request.Pets, fare, room.Capacity);

        if (!ReservationPaymentStatusRules.IsCancelled(requestedStatus.Code))
        {
            await EnsureNoConflictAsync(room.Id, entryDate, exitDate, reservation.Id, cancellationToken);
        }

        reservation.ClientId = client.Id;
        reservation.Client = client;
        reservation.RoomId = room.Id;
        reservation.Room = room;
        reservation.EntryDate = entryDate;
        reservation.ExitDate = exitDate;
        reservation.Adults = request.Adults;
        reservation.Children = request.Children;
        reservation.Infants = request.Infants;
        reservation.Pets = request.Pets;
        reservation.Fare = fare;
        reservation.StatusId = requestedStatus.Id;
        reservation.Status = requestedStatus;
        reservation.PromotorId = promotor.Id;
        reservation.Promotor = promotor;
        reservation.Notes = NormalizeNotes(request.Notes);

        await _financialService.ApplyPaymentStatusAsync(reservation, _financialService.Calculate(reservation.Movements), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _notifier.PublishReservationChangedAsync(reservation, "updated", cancellationToken);

        return await GetAsync(reservation.Id, cancellationToken);
    }

    public async Task CancelAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        var reservation = await _dbContext.Reservations
            .Include(candidate => candidate.Status)
            .SingleOrDefaultAsync(candidate => candidate.Id == reservationId, cancellationToken)
            ?? throw new NotFoundException("Reservation.NotFound", $"Reservation '{reservationId}' was not found.");

        var cancelled = await GetReservationStatusAsync(ReservationStatusCodes.Cancelled, cancellationToken);
        reservation.StatusId = cancelled.Id;
        reservation.Status = cancelled;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _notifier.PublishReservationChangedAsync(reservation, "cancelled", cancellationToken);
    }

    private static IQueryable<Reservation> ApplyDateFilters(IQueryable<Reservation> query, ReservationSearchRequest request)
    {
        if (request.From.HasValue != request.To.HasValue)
        {
            throw new ValidationException("Reservation.SearchDateRangeInvalid", "Both from and to are required when filtering by date.");
        }

        if (!request.From.HasValue || !request.To.HasValue)
        {
            return query;
        }

        var from = ReservationDateRules.ToUtcDate(request.From.Value);
        var to = ReservationDateRules.ToUtcDate(request.To.Value).AddDays(1);
        if (from >= to)
        {
            throw new ValidationException("Reservation.SearchDateRangeInvalid", "Search from date must be on or before to date.");
        }

        return request.DateMode switch
        {
            ReservationDateSearchMode.Calendar => query.Where(reservation => reservation.EntryDate < to && from < reservation.ExitDate),
            ReservationDateSearchMode.Arrivals => query.Where(reservation => reservation.EntryDate >= from && reservation.EntryDate < to),
            ReservationDateSearchMode.Departures => query.Where(reservation => reservation.ExitDate >= from && reservation.ExitDate < to),
            _ => throw new ValidationException("Reservation.SearchModeInvalid", "The reservation date search mode is invalid."),
        };
    }

    private static string EscapeLikePattern(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    private static IQueryable<Reservation> LoadReservationDetailQuery(IQueryable<Reservation> query)
    {
        return query
            .Include(reservation => reservation.Client)
            .Include(reservation => reservation.Room)
            .Include(reservation => reservation.Status)
            .Include(reservation => reservation.Movements)
                .ThenInclude(movement => movement.Concept)
            .Include(reservation => reservation.Movements)
                .ThenInclude(movement => movement.PaymentMethod)
            .Include(reservation => reservation.Movements)
                .ThenInclude(movement => movement.PaymentLocation);
    }

    private static string NormalizeStatusCode(string? statusCode)
    {
        return string.IsNullOrWhiteSpace(statusCode) ? ReservationStatusCodes.Pending : statusCode.Trim();
    }

    private static string? NormalizeNotes(string? notes)
    {
        return string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    private static void ValidateReservationValues(int adults, int children, int infants, int pets, decimal fare, int roomCapacity)
    {
        if (fare < 0m)
        {
            throw new ValidationException("Reservation.FareInvalid", "Fare must be non-negative.");
        }

        ReservationOccupancyRules.ValidateCapacity(adults, children, infants, pets, roomCapacity);
    }

    private async Task EnsureNoConflictAsync(int roomId, DateTime entryDate, DateTime exitDate, int? excludedReservationId, CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.Status)
            .Where(reservation => reservation.IsActive && reservation.RoomId == roomId)
            .Where(reservation => !excludedReservationId.HasValue || reservation.Id != excludedReservationId.Value)
            .Where(reservation => reservation.Status != null && reservation.Status.Code != ReservationStatusCodes.Cancelled)
            .ToListAsync(cancellationToken);

        var conflict = candidates.FirstOrDefault(candidate => ReservationDateRules.Overlaps(candidate.EntryDate, candidate.ExitDate, entryDate, exitDate));
        if (conflict is not null)
        {
            throw new ConflictException("Reservation.Conflict", "The room is already reserved for the requested dates.", new { reservationId = conflict.Id, roomId });
        }
    }

    private async Task<Room> GetActiveRoomAsync(int roomId, CancellationToken cancellationToken)
    {
        return await _dbContext.Rooms.SingleOrDefaultAsync(room => room.Id == roomId && room.IsActive, cancellationToken)
            ?? throw new NotFoundException("Room.NotFound", $"Room '{roomId}' was not found.");
    }

    private async Task<Client> GetActiveClientAsync(int clientId, CancellationToken cancellationToken)
    {
        return await _dbContext.Clients.SingleOrDefaultAsync(client => client.Id == clientId && client.IsActive, cancellationToken)
            ?? throw new NotFoundException("Client.NotFound", $"Client '{clientId}' was not found.");
    }

    private async Task<User> GetActiveUserAsync(int userId, string code, CancellationToken cancellationToken)
    {
        return await _dbContext.Users.SingleOrDefaultAsync(user => user.Id == userId && user.IsActive, cancellationToken)
            ?? throw new NotFoundException(code, $"User '{userId}' was not found.");
    }

    private async Task<ReservationStatus> GetReservationStatusAsync(string statusCode, CancellationToken cancellationToken)
    {
        return await _dbContext.ReservationStatuses.SingleOrDefaultAsync(status => status.Code == statusCode, cancellationToken)
            ?? throw new NotFoundException("ReservationStatus.NotFound", $"Reservation status '{statusCode}' was not found.");
    }

    private async Task<Concept> GetActiveConceptAsync(string conceptCode, CancellationToken cancellationToken)
    {
        return await _dbContext.Concepts.SingleOrDefaultAsync(concept => concept.Code == conceptCode && concept.IsActive, cancellationToken)
            ?? throw new NotFoundException("Concept.NotFound", $"Concept '{conceptCode}' was not found.");
    }

    private async Task<PaymentMethod> GetActivePaymentMethodAsync(string paymentMethodCode, CancellationToken cancellationToken)
    {
        return await _dbContext.PaymentMethods.SingleOrDefaultAsync(method => method.Code == paymentMethodCode && method.IsActive, cancellationToken)
            ?? throw new NotFoundException("PaymentMethod.NotFound", $"Payment method '{paymentMethodCode}' was not found.");
    }

    private async Task<PaymentLocation> GetActivePaymentLocationAsync(string paymentLocationCode, CancellationToken cancellationToken)
    {
        return await _dbContext.PaymentLocations.SingleOrDefaultAsync(location => location.Code == paymentLocationCode && location.IsActive, cancellationToken)
            ?? throw new NotFoundException("PaymentLocation.NotFound", $"Payment location '{paymentLocationCode}' was not found.");
    }
}
