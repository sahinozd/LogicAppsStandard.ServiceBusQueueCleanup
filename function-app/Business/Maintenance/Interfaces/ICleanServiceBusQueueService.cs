using Hip.Integrations.Shared.Functions.Models.Hip.ServiceBus;

namespace Hip.Integrations.Shared.Functions.Business.Maintenance.Interfaces;

public interface ICleanServiceBusQueueService
{
    Task ExecuteAsync(ServiceBusMessageContainer<IList<ServiceBusQueue>> input, CancellationToken cancellationToken);
}