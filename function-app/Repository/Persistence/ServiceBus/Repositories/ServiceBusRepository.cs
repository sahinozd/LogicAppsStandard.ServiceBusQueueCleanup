using Hip.Integrations.Shared.Functions.Business.Interfaces;
using Hip.Integrations.Shared.Functions.Models.Hip.ServiceBus;
using Hip.Integrations.Shared.Functions.Repository.Persistence.ServiceBus.Interfaces;

namespace Hip.Integrations.Shared.Functions.Repository.Persistence.ServiceBus.Repositories;

public class ServiceBusRepository : IServiceBusRepository
{
    private readonly IDataContext _dataContext;

    public ServiceBusRepository(IDataContext dataContext)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        _dataContext.InitializeClient();
    }

    public async Task<List<MessageInfo>> PeekActiveMessagesAsync(string queueName, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);

        return await _dataContext.PeekActiveMessagesAsync(queueName, null, batchSize, cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteSessionMessagesAsync(string queueName, IList<MessageInfo> messages, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);
        ArgumentNullException.ThrowIfNull(messages);

        await _dataContext.CompleteSessionMessagesAsync(queueName, messages, cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteDeferredSessionMessagesAsync(string queueName, IList<MessageInfo> messages, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);
        ArgumentNullException.ThrowIfNull(messages);

        await _dataContext.CompleteDeferredSessionMessagesAsync(queueName, messages, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeadLetterDeferredMessagesAsync(string queueName, IList<MessageInfo> messages, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);
        ArgumentNullException.ThrowIfNull(messages);

        await _dataContext.DeadLetterDeferredMessagesAsync(queueName, messages, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeadLetterRegularActiveMessagesAsync(string queueName, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);

        await _dataContext.DeadLetterActiveMessagesAsync(queueName, batchSize, cancellationToken).ConfigureAwait(false);
    }

    public async Task PurgeDeadLetterQueueAsync(string queueName, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);

        await _dataContext.PurgeDeadLetterQueueAsync(queueName, batchSize, cancellationToken).ConfigureAwait(false);
    }
}