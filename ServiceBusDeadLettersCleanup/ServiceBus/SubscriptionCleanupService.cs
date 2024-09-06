using System.Text.Json;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Options;
using ServiceBusDeadLettersCleanup.ServiceBus.Configs;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;

namespace ServiceBusDeadLettersCleanup.ServiceBus;

/// <summary>
/// SubscriptionCleanupService is a background service that listens to dead-letter queues
/// of Azure Service Bus topics and writes the dead-letter messages to Azure Blob Storage.
/// </summary>
public sealed class SubscriptionCleanupService(IOptions<BusConfig> busConfig, IOptions<StorageConfig> storageConfig)
    : BackgroundService, IAsyncDisposable
{
    // Blob container client for interacting with Azure Blob Storage
    private readonly BlobContainerClient _storageClient =
        new(storageConfig.Value.ConnectionString, storageConfig.Value.ContainerName);

    // Service Bus administration client for managing Service Bus resources
    private readonly ServiceBusAdministrationClient _busAdminClient = new(busConfig.Value.ConnectionString);

    // Service Bus client for interacting with Service Bus
    private readonly ServiceBusClient _busClient = new(busConfig.Value.ConnectionString);
    private readonly Dictionary<string, ServiceBusProcessor> _processors = new();

    /// <summary>
    /// Starts listening to the dead-letter queue of a specific topic and subscription.
    /// </summary>
    /// <param name="topicName">The name of the topic.</param>
    /// <param name="subscriptionName">The name of the subscription.</param>
    private async Task StartListeningToDeadLetterQueueAsync(string topicName, string subscriptionName)
    {
        Console.WriteLine($"Processing: {topicName}/{subscriptionName}");

        // Construct the dead-letter queue path
        var deadLetterPath = $"{topicName}/Subscriptions/{subscriptionName}/$DeadLetterQueue";
        // Create a processor for the dead-letter queue
        var processor = _busClient.CreateProcessor(deadLetterPath,
            new ServiceBusProcessorOptions
                { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete, PrefetchCount = 10, });
        _processors.Add($"{topicName}-{subscriptionName}", processor);

        // Event handler for processing messages
        processor.ProcessMessageAsync += async args =>
        {
            // Write the message to Azure Blob Storage
            await WriteMessageToBlobAsync(topicName, subscriptionName, args.Message);
            // Complete the message to remove it from the queue
            await args.CompleteMessageAsync(args.Message);
        };

        // Event handler for processing errors
        processor.ProcessErrorAsync += args =>
        {
            Console.WriteLine($"Error processing message: {args.Exception.Message}");
            //TODO: remove processor for not found sub.
            return Task.CompletedTask;
        };

        // Start processing messages
        await processor.StartProcessingAsync();
        Console.WriteLine($"Started listening to DLQ: {topicName}/{subscriptionName}");
    }

    /// <summary>
    /// Writes a dead-letter message to Azure Blob Storage.
    /// </summary>
    /// <param name="topicName">The name of the topic.</param>
    /// <param name="subscriptionName">The name of the subscription.</param>
    /// <param name="message">The received Service Bus message.</param>
    private async Task WriteMessageToBlobAsync(string topicName, string subscriptionName,
        ServiceBusReceivedMessage message)
    {
        // Construct the blob name using the topic, subscription, and message ID
        var blobName = $"topics/{topicName}/{subscriptionName}/{message.MessageId}.json";
        var blobClient = _storageClient.GetBlobClient(blobName);

        var data = message.ToMessage();
        await using var stream = data.ToStream();
        await blobClient.UploadAsync(stream, overwrite: true);

        Console.WriteLine($"Dead-letter message written to blob {blobName}");
    }

    /// <summary>
    /// Main execution method for the background service.
    /// </summary>
    /// <param name="stoppingToken">Token to signal the stopping of the service.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ensure the blob container exists
        await _storageClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

        // Retrieve all topics from the Service Bus
        var topics = _busAdminClient.GetTopicsAsync(stoppingToken).AsPages(pageSizeHint: 10);
        await foreach (var tps in topics)
        foreach (var tp in tps.Values)
        {
            // Retrieve all subscriptions for each topic
            var subscriptions = _busAdminClient.GetSubscriptionsAsync(tp.Name, stoppingToken)
                .AsPages(pageSizeHint: 10);
            await foreach (var subs in subscriptions)
            foreach (var sub in subs.Values)
                // Start listening to the dead-letter queue for each subscription
                await StartListeningToDeadLetterQueueAsync(tp.Name, sub.SubscriptionName);
        }
    }

    /// <summary>
    /// Method to stop the background service.
    /// </summary>
    /// <param name="stoppingToken">Token to signal the stopping of the service.</param>
    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        foreach (var processor in _processors)
        {
            await processor.Value.StartProcessingAsync(stoppingToken);
            await processor.Value.DisposeAsync();
        }

        _processors.Clear();
        // Dispose of the Service Bus client
        await _busClient.DisposeAsync();
        await base.StopAsync(stoppingToken);
    }

    private async ValueTask DisposeAsyncCore() => await StopAsync(CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }
}