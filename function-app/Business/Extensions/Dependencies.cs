using Hip.Integrations.Shared.Functions.Business.Maintenance.Interfaces;
using Hip.Integrations.Shared.Functions.Business.Maintenance.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Hip.Integrations.Shared.Functions.Business.Extensions;

[ExcludeFromCodeCoverage]
public static class Dependencies
{
    public static IServiceCollection RegisterBusinessDependencies(this IServiceCollection services)
    {
        //Maintenance
        services.AddScoped<ICleanServiceBusQueueService, CleanServiceBusQueueService>();

        return services;
    }
}
