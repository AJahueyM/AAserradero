using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.Reports;
using Microsoft.Extensions.DependencyInjection;

namespace AntiguoAserradero.Infrastructure.Reports;

public sealed class ReportsInfrastructureServiceModule : IServiceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddScoped<IReportSpreadsheetExporter, ClosedXmlReportSpreadsheetExporter>();
    }
}
