using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Errors;

namespace AntiguoAserradero.Application.Reservations;

public static class ReservationDateRules
{
    public static DateTime ToUtcDate(DateTime value)
    {
        var date = value.Date;
        return date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }

    public static bool IsValidRange(DateTime entryDate, DateTime exitDate)
    {
        return ToUtcDate(entryDate) < ToUtcDate(exitDate);
    }

    public static int CountNights(DateTime entryDate, DateTime exitDate)
    {
        var entry = ToUtcDate(entryDate);
        var exit = ToUtcDate(exitDate);
        if (entry >= exit)
        {
            throw new ValidationException("Reservation.InvalidDateRange", "Entry date must be before exit date.");
        }

        return (exit - entry).Days;
    }

    public static bool Overlaps(DateTime firstEntryDate, DateTime firstExitDate, DateTime secondEntryDate, DateTime secondExitDate)
    {
        var firstEntry = ToUtcDate(firstEntryDate);
        var firstExit = ToUtcDate(firstExitDate);
        var secondEntry = ToUtcDate(secondEntryDate);
        var secondExit = ToUtcDate(secondExitDate);

        return firstEntry < secondExit && secondEntry < firstExit;
    }
}

public static class ReservationOccupancyRules
{
    public static int CountOccupantsForCapacity(int adults, int children, int infants)
    {
        return adults + children + infants;
    }

    public static void ValidateNonNegative(int adults, int children, int infants, int pets)
    {
        if (adults < 0 || children < 0 || infants < 0 || pets < 0)
        {
            throw new ValidationException("Reservation.OccupantsInvalid", "Occupant counts must be non-negative.");
        }
    }

    public static void ValidateCapacity(int adults, int children, int infants, int pets, int capacity)
    {
        ValidateNonNegative(adults, children, infants, pets);
        if (CountOccupantsForCapacity(adults, children, infants) > capacity)
        {
            throw new ValidationException("Reservation.CapacityExceeded", "Occupants exceed the room capacity.", new { capacity });
        }
    }
}

public static class ReservationPaymentStatusRules
{
    public static bool IsCancelled(string statusCode)
    {
        return string.Equals(statusCode, ReservationStatusCodes.Cancelled, StringComparison.Ordinal);
    }

    public static bool IsPaymentDerivedStatus(string statusCode)
    {
        return string.Equals(statusCode, ReservationStatusCodes.Pending, StringComparison.Ordinal)
            || string.Equals(statusCode, ReservationStatusCodes.Partial, StringComparison.Ordinal)
            || string.Equals(statusCode, ReservationStatusCodes.Paid, StringComparison.Ordinal);
    }

    public static bool IsProtectedFromPaymentStatus(string statusCode)
    {
        return string.Equals(statusCode, ReservationStatusCodes.Maintenance, StringComparison.Ordinal)
            || string.Equals(statusCode, ReservationStatusCodes.Courtesy, StringComparison.Ordinal)
            || string.Equals(statusCode, ReservationStatusCodes.Cancelled, StringComparison.Ordinal);
    }

    public static string DeriveStatusCode(decimal charges, decimal payments)
    {
        if (payments <= 0m)
        {
            return ReservationStatusCodes.Pending;
        }

        if (charges > 0m && payments >= charges)
        {
            return ReservationStatusCodes.Paid;
        }

        if (payments > 0m && payments < charges)
        {
            return ReservationStatusCodes.Partial;
        }

        return ReservationStatusCodes.Pending;
    }
}
