namespace ServiceBusDeadLettersCleanup.ServiceBus;

public class Message(string body)
{
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Body { get; init; } = body;
    public string? DeadLetterReason { get; init; }
    public string? DeadLetterErrorDescription { get; init; }
    public DateTimeOffset EnqueuedTime { get; init; }
    public DateTimeOffset DeadLetterTime { get; init; }
    public int DeliveryCount { get; init; }
}