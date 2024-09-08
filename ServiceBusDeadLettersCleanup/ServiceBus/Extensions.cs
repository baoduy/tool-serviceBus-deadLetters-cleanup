using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace ServiceBusDeadLettersCleanup.ServiceBus;

public static class Extensions
{
    public static Message ToMessage(this ServiceBusReceivedMessage busMessage)
    {
        var message = new Message(busMessage.Body.ToString());
        message.Headers.Add("MessageId", busMessage.MessageId);
        message.Headers.Add("Subject", busMessage.Subject);
        foreach (var property in busMessage.ApplicationProperties)
        {
            if (property.Value is not null)
                message.Headers.TryAdd(property.Key, property.Value!.ToString() ?? string.Empty);
        }

        return message;
    }

    public static Stream ToStream(this Message message)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(message, new JsonSerializerOptions { WriteIndented = false });
        return new MemoryStream(data);
    }
}