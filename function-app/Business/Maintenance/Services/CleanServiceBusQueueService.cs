using Hip.Integrations.Shared.Functions.Business.Interfaces;
using Hip.Integrations.Shared.Functions.Business.Maintenance.Interfaces;
using Hip.Integrations.Shared.Functions.Models.Hip.ServiceBus;

namespace Hip.Integrations.Shared.Functions.Business.Maintenance.Services;

public class CleanServiceBusQueueService : ICleanServiceBusQueueService
{
    private readonly IServiceBusRepository _serviceBusRepository;


    public CleanServiceBusQueueService(IServiceBusRepository serviceBusRepository)
    {
        _serviceBusRepository = serviceBusRepository ?? throw new ArgumentNullException(nameof(serviceBusRepository));
    }

    public async Task ExecuteAsync(ServiceBusMessageContainer<IList<ServiceBusQueue>> input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var batchSize = input.BatchSize is null or < 1 ? 100 : input.BatchSize.Value;
        var handleSessionAndDeferredMessages = input.HandleSessionAndDeferredMessages is true;
        var deadletterRegularActiveQueueMessages = input.DeadletterActiveQueueMessages is true;
        var clearDeadletterQueueMessages = input.ClearDeadletterQueueMessages is true;
        var queues = input.Data;

        if (queues is not { Count: > 0 })
        {
            return;
        }

        await Parallel.ForEachAsync(queues.Distinct(), new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = cancellationToken
        },
            async (queue, token) =>
            {
                // If we need to handle session, session deferred and deferred messages, it will be done here
                if (handleSessionAndDeferredMessages)
                {
                    await HandleSessionAndDeferredMessages(queue, batchSize, token).ConfigureAwait(false);
                }

                // If the input message contains a property to deadletter the active messages, then go ahead and deadletter them
                if (deadletterRegularActiveQueueMessages)
                {
                    await _serviceBusRepository.DeadLetterRegularActiveMessagesAsync(queue.Name!, batchSize, token).ConfigureAwait(false);
                }

                // If the input message contains a property to clear the dlq messages, then go ahead purge them
                if (clearDeadletterQueueMessages)
                {
                    await _serviceBusRepository.PurgeDeadLetterQueueAsync(queue.Name!, batchSize, token).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
    }

    private async Task HandleSessionAndDeferredMessages(ServiceBusQueue queue, int batchSize, CancellationToken token)
    {
        var result = await _serviceBusRepository.PeekActiveMessagesAsync(queue.Name!, batchSize, token).ConfigureAwait(false);

        //First find session messages that are deferred and handle them
        var sessionDeferred = result.Where(x => x.IsDeferred && !string.IsNullOrEmpty(x.SessionId)).ToList();
        if (sessionDeferred.Count > 0)
        {
            await _serviceBusRepository.CompleteDeferredSessionMessagesAsync(queue.Name!, sessionDeferred, token).ConfigureAwait(false);
        }

        //Then find deferred messages and handle them
        var deferred = result.Where(x => x.IsDeferred && string.IsNullOrEmpty(x.SessionId)).ToList();
        if (deferred.Count > 0)
        {
            await _serviceBusRepository.DeadLetterDeferredMessagesAsync(queue.Name!, deferred, token).ConfigureAwait(false);
        }

        //Finally handle the session messages
        var session = result.Where(x => !x.IsDeferred && !string.IsNullOrEmpty(x.SessionId)).ToList();
        if (session.Count > 0)
        {
            await _serviceBusRepository.CompleteSessionMessagesAsync(queue.Name!, session, token).ConfigureAwait(false);
        }
    }
}