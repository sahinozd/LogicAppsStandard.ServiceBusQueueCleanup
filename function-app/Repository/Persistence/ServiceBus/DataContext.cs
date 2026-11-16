using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Hip.Integrations.Shared.Functions.Models.Hip.ServiceBus;
using Hip.Integrations.Shared.Functions.Repository.Persistence.ServiceBus.Interfaces;
using Hip.Integrations.Shared.Functions.Repository.Persistence.ServiceBus.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Hip.Integrations.Shared.Functions.Repository.Persistence.ServiceBus;

[ExcludeFromCodeCoverage(Justification = "Actual service bus connection can't be mocked")]
public class DataContext : IAsyncDisposable, IDataContext
{
    private readonly IConfiguration _configuration;
    private ServiceBusClient? _client;
    private ServiceBusAdministrationClient? _administrationClient;

    private readonly ILogger<ServiceBusRepository> _logger;
    private const string AppSettingKey = "serviceBusNamespace";

    public DataContext(IConfiguration configuration, ILogger<ServiceBusRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _configuration = configuration;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void InitializeClient()
    {
        var serviceBusNamespace = _configuration[AppSettingKey];
        ArgumentNullException.ThrowIfNull(serviceBusNamespace);

        var options = new ServiceBusClientOptions
        {
            TransportType = ServiceBusTransportType.AmqpWebSockets
        };

        _client = new ServiceBusClient(serviceBusNamespace, new DefaultAzureCredential(), options);
        _administrationClient = new ServiceBusAdministrationClient(serviceBusNamespace, new DefaultAzureCredential());

        _logger.LogInformation("ServiceBusClient and ServiceBusAdministrationClient initialized for namespace {ServiceBusNamespace}", serviceBusNamespace);
    }

    public async Task<List<MessageInfo>> PeekActiveMessagesAsync(string queueName, ServiceBusReceiverOptions? options, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);

        return await ExecuteWithResilienceAsync(async client =>
        {
            long? currentSequenceNumber = 0;
            var singleBatchSize = batchSize < 250 ? batchSize : 250;
            var totalMessagesRetrieved = 0;
            var allRetrievedMessages = new List<MessageInfo>();

            _logger.LogInformation("Peeking messages on queue: {QueueName}", queueName);
            var receiver = options != null ? client.CreateReceiver(queueName, options) : client.CreateReceiver(queueName);

            // We need to implement this kind of logic, because peeking may return max 250 records each time, we need to build this up until we reach the batch size.
            while (!cancellationToken.IsCancellationRequested && totalMessagesRetrieved < batchSize)
            {
                // Peek after the last sequence number, or from start if null
                var messages = await receiver.PeekMessagesAsync(singleBatchSize, currentSequenceNumber, cancellationToken).ConfigureAwait(false);
                if (messages == null || messages.Count == 0)
                {
                    // No more messages available
                    break;
                }

                allRetrievedMessages.AddRange(messages.Select(message => new MessageInfo
                {
                    MessageId = message.MessageId,
                    SequenceNumber = message.SequenceNumber,
                    EnqueuedTime = message.EnqueuedTime,
                    SessionId = message.SessionId,
                    IsDeferred = message.State == ServiceBusMessageState.Deferred
                })
                    .ToList());

                totalMessagesRetrieved += messages.Count;
                currentSequenceNumber = messages.ToList().Last().SequenceNumber + 1;

                // We need a small delay, otherwise service bus wil only return one single message at a time
                await Task.Delay(750, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Messages found on queue: {Count}", allRetrievedMessages.Count);
            await receiver.CloseAsync(cancellationToken).ConfigureAwait(false);

            return allRetrievedMessages.Distinct().ToList();
        }).ConfigureAwait(false);
    }

    public async Task CompleteSessionMessagesAsync(string queueName, IList<MessageInfo> messages, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);
        ArgumentNullException.ThrowIfNull(messages);

        await ExecuteWithResilienceAsync(async client =>
        {
            var sessionGroups = messages
                .Where(m => !string.IsNullOrEmpty(m.SessionId))
                .GroupBy(m => m.SessionId)
                .ToList();

            foreach (var group in sessionGroups)
            {
                ServiceBusSessionReceiver? receiver = null;

                try
                {
                    receiver = await client.AcceptSessionAsync(queueName, group.Key, cancellationToken: cancellationToken).ConfigureAwait(false);

                    // Receive in batches until the session is empty. Don't use the ReceiveMessagesAsync(CancellationToken) overload:
                    // that IAsyncEnumerable keeps polling forever, until the session lock expires and it throws SessionLockLost.
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var received = await receiver.ReceiveMessagesAsync(100, TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                        if (received.Count == 0)
                        {
                            break;
                        }

                        foreach (var message in received)
                        {
                            try
                            {
                                await receiver.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError("Session message with sessionId {GroupKey}, messageId {MessageMessageId} could not be completed from {QueueName}. {ExMessage}", group.Key, message.MessageId, queueName, ex.Message);
                            }
                        }
                    }
                }
                finally
                {
                    if (receiver != null)
                    {
                        await receiver.CloseAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

            }

            _logger.LogInformation("CompleteSessionMessagesAsync completed {MessagesCount} messages on queue {QueueName}", messages.Count, queueName);

            return 0; // dummy return
        }).ConfigureAwait(false);
    }

    public async Task CompleteDeferredSessionMessagesAsync(string queueName, IList<MessageInfo> messages, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);
        ArgumentNullException.ThrowIfNull(messages);

        await ExecuteWithResilienceAsync(async client =>
        {
            var sessionGroups = messages
                .Where(m => !string.IsNullOrEmpty(m.SessionId))
                .GroupBy(m => m.SessionId)
                .ToList();

            foreach (var group in sessionGroups)
            {
                ServiceBusSessionReceiver? receiver = null;

                try
                {
                    receiver = await client.AcceptSessionAsync(queueName, group.Key, cancellationToken: cancellationToken).ConfigureAwait(false);
                    var sequenceNumbers = group.Select(x => x.SequenceNumber);

                    foreach (var sequenceNumber in sequenceNumbers)
                    {
                        try
                        {
                            var message = await receiver.ReceiveDeferredMessageAsync(sequenceNumber, cancellationToken).ConfigureAwait(false);
                            if (message != null)
                            {
                                await receiver.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("Deferred session message with sequenceNumber {SequenceNumber} could not be retrieved or completed from {QueueName}. {ExMessage}", sequenceNumber, queueName, ex.Message);
                        }
                    }
                }
                finally
                {
                    if (receiver != null)
                    {
                        await receiver.CloseAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

            }

            _logger.LogInformation("CompleteDeferredSessionMessagesAsync completed {MessagesCount} messages on queue {QueueName}", messages.Count, queueName);

            return 0; // dummy return
        }).ConfigureAwait(false);
    }

    public async Task DeadLetterDeferredMessagesAsync(string queueName, IList<MessageInfo> messages, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);
        ArgumentNullException.ThrowIfNull(messages);

        await ExecuteWithResilienceAsync(async client =>
        {
            ServiceBusReceiver? receiver = null;

            try
            {
                receiver = client.CreateReceiver(queueName);
                foreach (var sequenceNumber in messages.Select(x => x.SequenceNumber))
                {
                    try
                    {
                        var message = await receiver.ReceiveDeferredMessageAsync(sequenceNumber, cancellationToken).ConfigureAwait(false);
                        if (message != null)
                        {
                            await receiver.DeadLetterMessageAsync(message, "Deadlettered by HIP maintenance logic", null, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Deferred message {SequenceNumber} could not be retrieved from {QueueName}. {ExMessage}", sequenceNumber, queueName, ex.Message);
                    }
                }
            }
            finally
            {
                if (receiver != null)
                {
                    await receiver.CloseAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            _logger.LogInformation("DeadLetterDeferredMessagesAsync completed {MessagesCount} messages on queue {QueueName}", messages.Count, queueName);

            return 0; // dummy return
        }).ConfigureAwait(false);
    }

    public async Task DeadLetterActiveMessagesAsync(string queueName, int batchSize, CancellationToken cancellationToken)
    {
        await ExecuteWithResilienceAsync(async client =>
        {
            if (await IsSessionBasedQueue(queueName, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Queue {QueueName} is a session based queue. Nothing to clear with DeadLetterActiveMessagesAsync method.", queueName);
                return 0;
            }

            var messages = await PeekActiveMessagesAsync(queueName, null, batchSize, cancellationToken).ConfigureAwait(false);
            if (messages.Any(x => x.IsDeferred) || messages.Count == 0)
            {
                _logger.LogInformation("Queue {QueueName} has deferred messages. Nothing to clear with DeadLetterActiveMessagesAsync method.", queueName);
                return 0;
            }

            ServiceBusReceiver? receiver = null;

            try
            {
                receiver = client.CreateReceiver(queueName);
                for (var i = 0; i < messages.Count; i++)
                {
                    try
                    {
                        var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                        if (message != null)
                        {
                            await receiver.DeadLetterMessageAsync(message, "Deadlettered by HIP maintenance logic", null, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Message could not be deadlettered from {QueueName}. {ExMessage}", queueName, ex.Message);
                    }
                }
            }
            finally
            {
                if (receiver != null)
                {
                    await receiver.CloseAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            _logger.LogInformation("DeadLetterActiveMessagesAsync deadlettered {MessagesCount} messages on queue {QueueName}", messages.Count, queueName);

            return 0; // dummy return
        }).ConfigureAwait(false);
    }

    public async Task PurgeDeadLetterQueueAsync(string queueName, int batchSize, CancellationToken cancellationToken)
    {
        await ExecuteWithResilienceAsync(async client =>
        {
            var options = new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter
            };

            var messages = await PeekActiveMessagesAsync(queueName, options, batchSize, cancellationToken).ConfigureAwait(false);

            if (messages.Count == 0)
            {
                _logger.LogInformation("Queue {QueueName} has no messages. Nothing to clear with DeadLetterActiveMessagesAsync method.", queueName);
                return 0;
            }

            ServiceBusReceiver? receiver = null;

            try
            {
                receiver = client.CreateReceiver(queueName, options);

                for (var i = 0; i < messages.Count; i++)
                {
                    try
                    {
                        var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

                        if (message != null)
                        {
                            await receiver.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Message could not be purged from {QueueName}. {ExMessage}", queueName, ex.Message);
                    }
                }
            }
            finally
            {
                if (receiver != null)
                {
                    await receiver.CloseAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            _logger.LogInformation("PurgeDeadLetterQueueAsync purged {MessagesCount} messages on queue {QueueName}", messages.Count, queueName);

            return 0;
        }).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is { IsClosed: false })
        {
            _logger.LogInformation("Disposing ServiceBusClient...");
            await _client.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    private ServiceBusClient EnsureClient()
    {
        if (_client is { IsClosed: false })
        {
            return _client!;
        }

        _logger.LogWarning("ServiceBusClient was closed or null. Reinitializing client");
        InitializeClient();

        return _client!;
    }

    private async Task<T> ExecuteWithResilienceAsync<T>(Func<ServiceBusClient, Task<T>> operation)
    {
        try
        {
            return await operation(EnsureClient()).ConfigureAwait(false);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "ServiceBusClient was disposed. Reinitializing...");
            InitializeClient();
            return await operation(_client!).ConfigureAwait(false);
        }
        catch (ServiceBusException ex) when (
            ex.Reason == ServiceBusFailureReason.ServiceCommunicationProblem ||
            ex.Reason == ServiceBusFailureReason.ServiceTimeout)
        {
            _logger.LogWarning(ex, "ServiceBusClient connection issue detected. Reinitializing...");
            InitializeClient();
            return await operation(_client!).ConfigureAwait(false);
        }
    }

    private async Task<bool> IsSessionBasedQueue(string queueName, CancellationToken cancellationToken)
    {
        var isSessionBasedQueue = false;

        var queue = await _administrationClient!.GetQueueAsync(queueName, cancellationToken).ConfigureAwait(false);
        if (queue.Value.RequiresSession)
        {
            isSessionBasedQueue = true;
        }

        return isSessionBasedQueue;
    }
}