namespace AntiguoAserradero.Application.Reports;

public sealed record SpreadsheetFile(string FileName, string ContentType, byte[] Content);

public interface IReportSpreadsheetExporter
{
    Task<SpreadsheetFile> ExportTableAsync(ExportTable table, CancellationToken cancellationToken = default);

    Task<SpreadsheetFile> ExportOccupancyFinancialReportAsync(OccupancyFinancialReport report, CancellationToken cancellationToken = default);
}
