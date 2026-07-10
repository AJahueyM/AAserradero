using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.Areas;
using AntiguoAserradero.Application.Concepts;
using AntiguoAserradero.Application.Configuration;
using AntiguoAserradero.Application.Rooms;
using Microsoft.Extensions.DependencyInjection;

namespace AntiguoAserradero.Application.Reference;

public sealed class CatalogServiceModule : IServiceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddScoped<IAreaService, AreaService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IConceptService, ConceptService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<IConfigValueService, ConfigValueService>();
    }
}
