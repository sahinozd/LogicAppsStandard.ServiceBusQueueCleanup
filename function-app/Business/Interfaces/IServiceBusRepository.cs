using Hip.Integrations.Shared.Functions.Models.Hip.ServiceBus;

namespace Hip.Integrations.Shared.Functions.Business.Interfaces;

public interface IServiceBusRepository
{
    Task<List<MessageInfo>> PeekActiveMessagesAsync(string queueName, int batchSize, CancellationToken cancellationToken);

    Task CompleteSessionMessagesAsync(string queueName, IList<MessageInfo> messages, CancellationToken cancellationToken);

    Task CompleteDeferredSessionMessagesAsync(string queueName, IList<MessageInfo> messages, CancellationToken cancellationToken);

    Task DeadLetterDeferredMessagesAsync(string queueName, IList<MessageInfo> messages, CancellationToken cancellationToken);

    Task DeadLetterRegularActiveMessagesAsync(string queueName, int batchSize, CancellationToken cancellationToken);

    Task PurgeDeadLetterQueueAsync(string queueName, int batchSize, CancellationToken cancellationToken);
}