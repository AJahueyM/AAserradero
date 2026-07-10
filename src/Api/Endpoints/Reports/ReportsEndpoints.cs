using AntiguoAserradero.Application.Auth;
using AntiguoAserradero.Application.Reports;
using AntiguoAserradero.Domain.Errors;

namespace AntiguoAserradero.Api.Endpoints.Reports;

public sealed class ReportsEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reports").RequireAuthorization();

        group.MapGet("/exports/metadata", (ICurrentUser currentUser, IReportService reports) =>
            {
                EnsureReportsPermission(currentUser);
                return Results.Ok(reports.GetExportMetadata());
            })
            .WithName("GetExportMetadata");

        group.MapPost("/exports", async (ExportRequest request, ICurrentUser currentUser, IReportService reports, IReportSpreadsheetExporter exporter, CancellationToken cancellationToken) =>
            {
                EnsureReportsPermission(currentUser);
                var file = await exporter.ExportTableAsync(reports.CreateExport(request), cancellationToken);
                return Results.File(file.Content, file.ContentType, file.FileName);
            })
            .WithName("ExportData");

        group.MapPost("/occupancy-financial/export", async (OccupancyFinancialReportRequest request, ICurrentUser currentUser, IReportService reports, IReportSpreadsheetExporter exporter, CancellationToken cancellationToken) =>
            {
                EnsureReportsPermission(currentUser);
                var report = await reports.CreateOccupancyFinancialReportAsync(request, cancellationToken);
                var file = await exporter.ExportOccupancyFinancialReportAsync(report, cancellationToken);
                return Results.File(file.Content, file.ContentType, file.FileName);
            })
            .WithName("ExportOccupancyFinancialReport");
    }

    private static void EnsureReportsPermission(ICurrentUser currentUser)
    {
        if (!currentUser.Capabilities.Contains(ApplicationCapability.CatalogManage, StringComparer.Ordinal)
            && !currentUser.Capabilities.Contains(ApplicationCapability.ReservationsManage, StringComparer.Ordinal))
        {
            throw new ForbiddenException("Auth.Forbidden", "Se requiere permiso de catálogo o reservaciones para exportar reportes.");
        }
    }
}
