using Newtonsoft.Json;

namespace Hip.Integrations.Shared.Functions.Models.Hip.ServiceBus;

public record ServiceBusQueue
{
    [JsonProperty(nameof(Name))]
    public string? Name { get; set; }
}