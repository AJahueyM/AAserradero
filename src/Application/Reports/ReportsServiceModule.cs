using AntiguoAserradero.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AntiguoAserradero.Application.Reports;

public sealed class ReportsServiceModule : IServiceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddScoped<IReportService, ReportService>();
    }
}
