using Azure.Messaging.ServiceBus;
using Hip.Integrations.Shared.Functions.Models.Hip.ServiceBus;

namespace Hip.Integrations.Shared.Functions.Repository.Persistence.ServiceBus.Interfaces;

public interface IDataContext
{
    void InitializeClient();

    Task<List<MessageInfo>> PeekActiveMessagesAsync(string queueName, ServiceBusReceiverOptions? options, int batchSize, CancellationToken cancellationToken);

    Task CompleteSessionMessagesAsync(string queueName, IList<MessageInfo> messages, CancellationToken cancellationToken);

    Task CompleteDeferredSessionMessagesAsync(string queueName, IList<MessageInfo> messages, CancellationToken cancellationToken);

    Task DeadLetterDeferredMessagesAsync(string queueName, IList<MessageInfo> messages, CancellationToken cancellationToken);

    Task DeadLetterActiveMessagesAsync(string queueName, int batchSize, CancellationToken cancellationToken);

    Task PurgeDeadLetterQueueAsync(string queueName, int batchSize, CancellationToken cancellationToken);

    ValueTask DisposeAsync();
}