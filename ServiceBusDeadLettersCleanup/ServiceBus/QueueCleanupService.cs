using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Options;
using ServiceBusDeadLettersCleanup.ServiceBus.Configs;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;

namespace ServiceBusDeadLettersCleanup.ServiceBus;

/// <summary>
/// QueueCleanupService is a background service that listens to dead-letter queues
/// of Azure Service Bus queues and writes the dead-letter messages to Azure Blob Storage.
/// </summary>
public sealed class QueueCleanupService(
    IOptions<BusConfig> busConfig,
    IOptions<StorageConfig> storageConfig,
    ILogger<QueueCleanupService> logger)
    : BackgroundService, IAsyncDisposable
{
    private readonly BusConfig _busConfig = busConfig.Value;
    private readonly BlobContainerClient _storageClient =
        new(storageConfig.Value.ConnectionString, storageConfig.Value.ContainerName);
    private readonly ServiceBusAdministrationClient _busAdminClient = new(busConfig.Value.ConnectionString);
    private readonly ServiceBusClient _busClient = new(busConfig.Value.ConnectionString);
    private readonly Dictionary<string, ServiceBusProcessor> _processors = new();

    /// <summary>
    /// Starts listening to the dead-letter queue of a specific queue.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task StartListeningToDeadLetterQueueAsync(string queueName, CancellationToken cancellationToken)
    {
        logger.LogInformation("Setting up processor for queue: {QueueName}", queueName);

        var deadLetterPath = $"{queueName}/$DeadLetterQueue";
        var processor = _busClient.CreateProcessor(deadLetterPath,
            new ServiceBusProcessorOptions
            {
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
                PrefetchCount = _busConfig.PrefetchCount,
                MaxConcurrentCalls = _busConfig.MaxConcurrentCalls,
                AutoCompleteMessages = false
            });
        _processors.Add(queueName, processor);

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                await WriteMessageToBlobAsync(queueName, args.Message, args.CancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write message {MessageId} from queue {QueueName} to blob storage",
                    args.Message.MessageId, queueName);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Error processing messages from queue {QueueName}. Error source: {ErrorSource}",
                queueName, args.ErrorSource);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(cancellationToken);
        logger.LogInformation("Started listening to DLQ for queue: {QueueName}", queueName);
    }

    /// <summary>
    /// Writes a dead-letter message to Azure Blob Storage.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <param name="message">The received Service Bus message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task WriteMessageToBlobAsync(string queueName, ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var blobName = $"queues/{queueName}/{now:yyyy/MM/dd}/{message.MessageId}.json";
        var blobClient = _storageClient.GetBlobClient(blobName);

        var data = message.ToMessage();
        await using var stream = data.ToStream();
        
        var metadata = new Dictionary<string, string>
        {
            ["DeadLetterReason"] = message.DeadLetterReason ?? "Unknown",
            ["EnqueuedTime"] = message.EnqueuedTime.ToString("O"),
            ["QueueName"] = queueName
        };
        
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);
        await blobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);

        logger.LogInformation("Dead-letter message {MessageId} from queue {QueueName} written to blob: {BlobName}",
            message.MessageId, queueName, blobName);
    }

    /// <summary>
    /// Main execution method for the background service.
    /// </summary>
    /// <param name="stoppingToken">Token to signal the stopping of the service.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting QueueCleanupService...");
            
            await _storageClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
            logger.LogInformation("Blob container verified: {ContainerName}", _storageClient.Name);

            var queues = _busAdminClient.GetQueuesAsync(stoppingToken).AsPages(pageSizeHint: 100);
            var queueCount = 0;
            
            await foreach (var page in queues)
            {
                foreach (var queue in page.Values)
                {
                    if (ShouldProcessQueue(queue.Name))
                    {
                        await StartListeningToDeadLetterQueueAsync(queue.Name, stoppingToken);
                        queueCount++;
                    }
                    else
                    {
                        logger.LogDebug("Skipping queue {QueueName} (filtered)", queue.Name);
                    }
                }
            }
            
            logger.LogInformation("QueueCleanupService started successfully. Monitoring {QueueCount} queue(s)", queueCount);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to start QueueCleanupService");
            throw;
        }
    }
    
    private bool ShouldProcessQueue(string queueName)
    {
        if (_busConfig.ExcludeQueues?.Contains(queueName) == true)
            return false;
            
        if (_busConfig.IncludeQueues?.Length > 0)
            return _busConfig.IncludeQueues.Contains(queueName);
            
        return true;
    }

    /// <summary>
    /// Method to stop the background service.
    /// </summary>
    /// <param name="stoppingToken">Token to signal the stopping of the service.</param>
    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        foreach (var processor in _processors)
        {
            await processor.Value.StopProcessingAsync(stoppingToken);
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