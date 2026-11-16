using Newtonsoft.Json;

namespace Hip.Integrations.Shared.Functions.Models.Hip.ServiceBus;

public record ServiceBusMessageContainer<T>
{
    [JsonProperty(nameof(Data))]
    public T? Data { get; set; }

    [JsonProperty(nameof(BatchSize))]
    public int? BatchSize { get; set; }

    [JsonProperty(nameof(HandleSessionAndDeferredMessages))]
    public bool? HandleSessionAndDeferredMessages { get; set; }

    [JsonProperty(nameof(DeadletterActiveQueueMessages))]
    public bool? DeadletterActiveQueueMessages { get; set; }

    [JsonProperty(nameof(ClearDeadletterQueueMessages))]
    public bool? ClearDeadletterQueueMessages { get; set; }
}