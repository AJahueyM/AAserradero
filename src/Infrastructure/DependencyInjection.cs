using AntiguoAserradero.Application;
using AntiguoAserradero.Application.Abstractions;
using AntiguoAserradero.Application.LiveUpdates;
using AntiguoAserradero.Infrastructure.LiveUpdates;
using AntiguoAserradero.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AntiguoAserradero.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<AntiguoAserraderoDbContext>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseSqlServer(SqlConnectionFactory.Create(databaseOptions.Default), sql => sql.EnableRetryOnFailure());
        });

        services.AddScoped<IApplicationDbContext>(serviceProvider => serviceProvider.GetRequiredService<AntiguoAserraderoDbContext>());

        services.AddSingleton<ILiveUpdateBroadcaster, InMemoryLiveUpdateBroadcaster>();
        services.AddSingleton<ILiveUpdatePublisher>(serviceProvider => serviceProvider.GetRequiredService<ILiveUpdateBroadcaster>());

        // Feature modules living in this assembly register their own services.
        services.AddServiceModulesFrom(typeof(DependencyInjection).Assembly);

        return services;
    }
}
