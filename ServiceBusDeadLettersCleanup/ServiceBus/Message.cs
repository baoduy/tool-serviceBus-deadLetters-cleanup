namespace ServiceBusDeadLettersCleanup.ServiceBus;

public class Message(string body)
{
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Body { get; init; } = body;
}