using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace ServiceBusDeadLettersCleanup.ServiceBus;

public static class Extensions
{
    public static Message ToMessage(this ServiceBusReceivedMessage busMessage)
    {
        var message = new Message(Convert.ToBase64String(busMessage.Body.ToArray()))
        {
            DeadLetterReason = busMessage.DeadLetterReason,
            DeadLetterErrorDescription = busMessage.DeadLetterErrorDescription,
            EnqueuedTime = busMessage.EnqueuedTime,
            DeadLetterTime = DateTimeOffset.UtcNow,
            DeliveryCount = busMessage.DeliveryCount
        };
        
        message.Headers.Add("MessageId", busMessage.MessageId);
        message.Headers.Add("Subject", busMessage.Subject ?? string.Empty);
        message.Headers.Add("ContentType", busMessage.ContentType ?? string.Empty);
        message.Headers.Add("CorrelationId", busMessage.CorrelationId ?? string.Empty);
        
        foreach (var property in busMessage.ApplicationProperties)
        {
            if (property.Value is not null)
                message.Headers.TryAdd(property.Key, property.Value!.ToString() ?? string.Empty);
        }

        return message;
    }

    public static Stream ToStream(this Message message)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var data = JsonSerializer.SerializeToUtf8Bytes(message, options);
        return new MemoryStream(data);
    }
}