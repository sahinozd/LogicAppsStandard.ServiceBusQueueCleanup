namespace Hip.Integrations.Shared.Functions.Models.Hip.ServiceBus;

public class MessageInfo
{
    public string? MessageId { get; set; }
    public long SequenceNumber { get; set; }
    public DateTimeOffset EnqueuedTime { get; set; }
    public string? SessionId { get; set; }
    public bool IsDeferred { get; set; }
}