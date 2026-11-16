using Hip.Integrations.Shared.Functions.Business.Interfaces;
using Hip.Integrations.Shared.Functions.Repository.Persistence.ServiceBus.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Hip.Integrations.Shared.Functions.Repository.Extensions;

[ExcludeFromCodeCoverage]
public static class Dependencies
{
    public static IServiceCollection RegisterRepositoryDependencies(this IServiceCollection services)
    {
        services.AddScoped<Persistence.ServiceBus.Interfaces.IDataContext, Persistence.ServiceBus.DataContext>();
        services.AddScoped<IServiceBusRepository, ServiceBusRepository>();

        return services;
    }
}
