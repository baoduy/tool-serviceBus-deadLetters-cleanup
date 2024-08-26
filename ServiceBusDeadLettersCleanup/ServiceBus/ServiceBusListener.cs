using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Options;
using ServiceBusDeadLettersCleanup.ServiceBus.Configs;

namespace ServiceBusDeadLettersCleanup.ServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using System;
using System.Text;
using System.Threading.Tasks;

public class ServiceBusBackgroundService(IOptions<BusConfig> busConfig, IOptions<StorageConfig> storageConfig)
    : BackgroundService
{
    //private readonly List<ServiceBusProcessor> _serviceBusProcessors = new();

    private readonly BlobContainerClient _storageClient =
        new(storageConfig.Value.ConnectionString, storageConfig.Value.ContainerName);

    private readonly ServiceBusAdministrationClient _busAdminClient = new(busConfig.Value.ConnectionString);
    private readonly ServiceBusClient _busClient = new ServiceBusClient(busConfig.Value.ConnectionString);

    private async Task StartListeningToDeadLetterQueueAsync(string topicName, string subscriptionName)
    {
        Console.WriteLine($"Processing: {topicName}/{subscriptionName}");

        var deadLetterPath = $"{topicName}/Subscriptions/{subscriptionName}/$DeadLetterQueue";
        var processor = _busClient.CreateProcessor(deadLetterPath, new ServiceBusProcessorOptions());

        processor.ProcessMessageAsync += async args =>
        {
            await WriteMessageToBlobAsync(topicName, subscriptionName, args.Message);
            await args.CompleteMessageAsync(args.Message);
        };

        processor.ProcessErrorAsync += args =>
        {
            Console.WriteLine($"Error processing message: {args.Exception.Message}");
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync();
        Console.WriteLine($"Started listening to DLQ: {topicName}/{subscriptionName}");
    }

    private async Task WriteMessageToBlobAsync(string topicName, string subscriptionName,
        ServiceBusReceivedMessage message)
    {
        var blobName = $"{topicName}/{subscriptionName}/{message.MessageId}.txt";
        var blobClient = _storageClient.GetBlobClient(blobName);

        await using (var stream = message.Body.ToStream())
            await blobClient.UploadAsync(stream, overwrite: true);

        Console.WriteLine($"Dead-letter message written to blob {blobName}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _storageClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

        var topics = _busAdminClient.GetTopicsAsync().AsPages(pageSizeHint: 10);
        await foreach (var tps in topics)
        {
            foreach (var tp in tps.Values)
            {
                var subscriptions = _busAdminClient.GetSubscriptionsAsync(tp.Name).AsPages(pageSizeHint: 10);
                await foreach (var subs in subscriptions)
                {
                    foreach (var sub in subs.Values)
                    {
                        await StartListeningToDeadLetterQueueAsync(tp.Name, sub.SubscriptionName);
                    }
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        // foreach (var processor in _serviceBusProcessors)
        //     await processor.StopProcessingAsync(stoppingToken);
        //_serviceBusProcessors.Clear();
        await _busClient.DisposeAsync();
        await base.StopAsync(stoppingToken);
    }
}