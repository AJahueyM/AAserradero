using AntiguoAserradero.Application.Reports;
using ClosedXML.Excel;

namespace AntiguoAserradero.Infrastructure.Reports;

public sealed class ClosedXmlReportSpreadsheetExporter : IReportSpreadsheetExporter
{
    private const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<SpreadsheetFile> ExportTableAsync(ExportTable table, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Exportación");

        for (var column = 0; column < table.Fields.Count; column++)
        {
            worksheet.Cell(1, column + 1).Value = table.Fields[column];
        }

        var rowNumber = 2;
        await foreach (var row in table.Rows.WithCancellation(cancellationToken))
        {
            for (var column = 0; column < row.Count; column++)
            {
                worksheet.Cell(rowNumber, column + 1).Value = ToCellValue(row[column]);
            }

            rowNumber++;
        }

        worksheet.Columns().AdjustToContents();
        return Save(workbook, $"export-{table.Entity}-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    public Task<SpreadsheetFile> ExportOccupancyFinancialReportAsync(OccupancyFinancialReport report, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("Reporte");
        summary.Cell(1, 1).Value = ReportLabels.OccupancyFinancialTitle;
        summary.Cell(2, 1).Value = $"{report.StartDate:yyyy-MM-dd} - {report.EndDate:yyyy-MM-dd}";

        var headers = new[]
        {
            ReportLabels.Area,
            ReportLabels.Room,
            ReportLabels.OccupancyPercentage,
            ReportLabels.OccupiedDays,
            ReportLabels.CountedDays,
            ReportLabels.Nights,
            ReportLabels.ReservationCount,
            ReportLabels.Cancellations,
            ReportLabels.Occupants,
            ReportLabels.Income,
            ReportLabels.Discounts,
        };

        for (var column = 0; column < headers.Length; column++)
        {
            summary.Cell(4, column + 1).Value = headers[column];
        }

        var rowNumber = 5;
        foreach (var row in report.Rooms)
        {
            summary.Cell(rowNumber, 1).Value = row.Area;
            summary.Cell(rowNumber, 2).Value = row.Room;
            summary.Cell(rowNumber, 3).Value = row.OccupancyPercentage / 100m;
            summary.Cell(rowNumber, 3).Style.NumberFormat.Format = "0.00%";
            summary.Cell(rowNumber, 4).Value = row.OccupiedDays;
            summary.Cell(rowNumber, 5).Value = row.CountedDays;
            summary.Cell(rowNumber, 6).Value = row.Nights;
            summary.Cell(rowNumber, 7).Value = row.ReservationCount;
            summary.Cell(rowNumber, 8).Value = row.Cancellations;
            summary.Cell(rowNumber, 9).Value = row.Occupants;
            summary.Cell(rowNumber, 10).Value = row.Income;
            summary.Cell(rowNumber, 11).Value = row.Discounts;
            rowNumber++;
        }

        summary.Columns().AdjustToContents();

        var income = workbook.Worksheets.Add("Ingresos por fecha");
        income.Cell(1, 1).Value = ReportLabels.IncomeByPaymentDateTitle;
        income.Cell(3, 1).Value = ReportLabels.PaymentDate;
        income.Cell(3, 2).Value = ReportLabels.Income;
        rowNumber = 4;
        foreach (var row in report.IncomeByPaymentDate)
        {
            income.Cell(rowNumber, 1).Value = row.Key.ToDateTime(TimeOnly.MinValue);
            income.Cell(rowNumber, 2).Value = row.Value;
            rowNumber++;
        }

        income.Columns().AdjustToContents();
        return Task.FromResult(Save(workbook, $"reporte-ocupacion-finanzas-{report.StartDate:yyyyMMdd}-{report.EndDate:yyyyMMdd}.xlsx"));
    }

    private static XLCellValue ToCellValue(object? value)
    {
        return value switch
        {
            null => Blank.Value,
            DateTime dateTime => dateTime,
            DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
            TimeOnly timeOnly => timeOnly.ToString("HH:mm", ReportLabels.Culture),
            decimal decimalValue => decimalValue,
            int intValue => intValue,
            bool boolValue => boolValue,
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static SpreadsheetFile Save(XLWorkbook workbook, string fileName)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new SpreadsheetFile(fileName, ContentType, stream.ToArray());
    }
}
