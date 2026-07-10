using System.Globalization;

namespace AntiguoAserradero.Application.Reports;

public enum ReportPeriodType
{
    Monthly,
    Annual,
}

public sealed record ExportMetadata(IReadOnlyList<ExportEntityMetadata> Entities);

public sealed record ExportEntityMetadata(string Entity, IReadOnlyList<string> Fields);

public sealed record ExportRequest(string Entity, IReadOnlyList<string> Fields);

public sealed record ExportTable(string Entity, IReadOnlyList<string> Fields, IAsyncEnumerable<IReadOnlyList<object?>> Rows);

public sealed record OccupancyFinancialReportRequest(ReportPeriodType PeriodType, int Year, int? Month, IReadOnlyList<DayOfWeek> Weekdays);

public sealed record OccupancyFinancialReport(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<DayOfWeek> CountedWeekdays,
    IReadOnlyList<RoomOccupancyFinancialRow> Rooms,
    IReadOnlyDictionary<DateOnly, decimal> IncomeByPaymentDate);

public sealed record RoomOccupancyFinancialRow(
    int RoomId,
    string Room,
    string Area,
    decimal OccupancyPercentage,
    decimal OccupiedDays,
    int CountedDays,
    int Nights,
    int ReservationCount,
    int Cancellations,
    int Occupants,
    decimal Income,
    decimal Discounts);

public static class ReportLabels
{
    public const string OccupancyFinancialTitle = "Reporte de ocupación y finanzas";
    public const string IncomeByPaymentDateTitle = "Ingresos por fecha de pago";
    public const string Room = "Habitación";
    public const string Area = "Área";
    public const string OccupancyPercentage = "Ocupación %";
    public const string OccupiedDays = "Días ocupados";
    public const string CountedDays = "Días contados";
    public const string Nights = "Noches";
    public const string ReservationCount = "Reservaciones";
    public const string Cancellations = "Cancelaciones";
    public const string Occupants = "Ocupantes";
    public const string Income = "Ingresos";
    public const string Discounts = "Descuentos";
    public const string PaymentDate = "Fecha de pago";

    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-MX");
}
