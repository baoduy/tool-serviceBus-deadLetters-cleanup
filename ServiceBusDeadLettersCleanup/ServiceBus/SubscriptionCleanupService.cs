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
public sealed class SubscriptionCleanupService(
    IOptions<BusConfig> busConfig,
    IOptions<StorageConfig> storageConfig,
    ILogger<SubscriptionCleanupService> logger)
    : BackgroundService, IAsyncDisposable
{
    private readonly BusConfig _busConfig = busConfig.Value;
    private readonly BlobContainerClient _storageClient =
        new(storageConfig.Value.ConnectionString, storageConfig.Value.ContainerName);
    private readonly ServiceBusAdministrationClient _busAdminClient = new(busConfig.Value.ConnectionString);
    private readonly ServiceBusClient _busClient = new(busConfig.Value.ConnectionString);
    private readonly Dictionary<string, ServiceBusProcessor> _processors = new();

    /// <summary>
    /// Starts listening to the dead-letter queue of a specific topic and subscription.
    /// </summary>
    /// <param name="topicName">The name of the topic.</param>
    /// <param name="subscriptionName">The name of the subscription.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task StartListeningToDeadLetterQueueAsync(string topicName, string subscriptionName,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Setting up processor for topic/subscription: {TopicName}/{SubscriptionName}",
            topicName, subscriptionName);

        var deadLetterPath = $"{topicName}/Subscriptions/{subscriptionName}/$DeadLetterQueue";
        var processor = _busClient.CreateProcessor(deadLetterPath,
            new ServiceBusProcessorOptions
            {
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
                PrefetchCount = _busConfig.PrefetchCount,
                MaxConcurrentCalls = _busConfig.MaxConcurrentCalls,
                AutoCompleteMessages = false
            });
        _processors.Add($"{topicName}-{subscriptionName}", processor);

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                await WriteMessageToBlobAsync(topicName, subscriptionName, args.Message, args.CancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to write message {MessageId} from {TopicName}/{SubscriptionName} to blob storage",
                    args.Message.MessageId, topicName, subscriptionName);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception,
                "Error processing messages from {TopicName}/{SubscriptionName}. Error source: {ErrorSource}",
                topicName, subscriptionName, args.ErrorSource);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(cancellationToken);
        logger.LogInformation("Started listening to DLQ for topic/subscription: {TopicName}/{SubscriptionName}",
            topicName, subscriptionName);
    }

    /// <summary>
    /// Writes a dead-letter message to Azure Blob Storage.
    /// </summary>
    /// <param name="topicName">The name of the topic.</param>
    /// <param name="subscriptionName">The name of the subscription.</param>
    /// <param name="message">The received Service Bus message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task WriteMessageToBlobAsync(string topicName, string subscriptionName,
        ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var blobName = $"topics/{topicName}/{subscriptionName}/{now:yyyy/MM/dd}/{message.MessageId}.json";
        var blobClient = _storageClient.GetBlobClient(blobName);

        var data = message.ToMessage();
        await using var stream = data.ToStream();
        
        var metadata = new Dictionary<string, string>
        {
            ["DeadLetterReason"] = message.DeadLetterReason ?? "Unknown",
            ["EnqueuedTime"] = message.EnqueuedTime.ToString("O"),
            ["TopicName"] = topicName,
            ["SubscriptionName"] = subscriptionName
        };
        
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);
        await blobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);

        logger.LogInformation(
            "Dead-letter message {MessageId} from {TopicName}/{SubscriptionName} written to blob: {BlobName}",
            message.MessageId, topicName, subscriptionName, blobName);
    }

    /// <summary>
    /// Main execution method for the background service.
    /// </summary>
    /// <param name="stoppingToken">Token to signal the stopping of the service.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting SubscriptionCleanupService...");
            
            await _storageClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
            logger.LogInformation("Blob container verified: {ContainerName}", _storageClient.Name);

            var topics = _busAdminClient.GetTopicsAsync(stoppingToken).AsPages(pageSizeHint: 100);
            var subscriptionCount = 0;
            
            await foreach (var topicPage in topics)
            {
                foreach (var topic in topicPage.Values)
                {
                    if (!ShouldProcessTopic(topic.Name))
                    {
                        logger.LogDebug("Skipping topic {TopicName} (filtered)", topic.Name);
                        continue;
                    }

                    var subscriptions = _busAdminClient.GetSubscriptionsAsync(topic.Name, stoppingToken)
                        .AsPages(pageSizeHint: 100);
                    await foreach (var subPage in subscriptions)
                    {
                        foreach (var sub in subPage.Values)
                        {
                            await StartListeningToDeadLetterQueueAsync(topic.Name, sub.SubscriptionName,
                                stoppingToken);
                            subscriptionCount++;
                        }
                    }
                }
            }
            
            logger.LogInformation("SubscriptionCleanupService started successfully. Monitoring {SubscriptionCount} subscription(s)",
                subscriptionCount);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to start SubscriptionCleanupService");
            throw;
        }
    }
    
    private bool ShouldProcessTopic(string topicName)
    {
        if (_busConfig.ExcludeTopics?.Contains(topicName) == true)
            return false;
            
        if (_busConfig.IncludeTopics?.Length > 0)
            return _busConfig.IncludeTopics.Contains(topicName);
            
        return true;
    }

    /// <summary>
    /// Method to stop the background service.
    /// </summary>
    /// <param name="stoppingToken">Token to signal the stopping of the service.</param>
    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Stopping SubscriptionCleanupService...");
        
        foreach (var processor in _processors.Values)
        {
            try
            {
                if (processor.IsProcessing)
                {
                    await processor.StopProcessingAsync(stoppingToken);
                }
                await processor.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error stopping processor");
            }
        }

        _processors.Clear();
        await _busClient.DisposeAsync();
        await _busAdminClient.DisposeAsync();
        
        logger.LogInformation("SubscriptionCleanupService stopped");
        await base.StopAsync(stoppingToken);
    }

    private async ValueTask DisposeAsyncCore() => await StopAsync(CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }
}