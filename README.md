# ServiceBusDeadLettersCleanup

## Overview

ServiceBusDeadLettersCleanup is a background service designed to listen to dead-letter queues of Azure Service Bus topics and queues, then write the dead-letter messages to Azure Blob Storage. This ensures that messages that cannot be processed are stored for later analysis or reprocessing.

## Features

- Listens to dead-letter queues of Azure Service Bus topics and queues
- Writes dead-letter messages to Azure Blob Storage with hierarchical date-based naming
- Configurable via `appsettings.json`
- Structured logging with Microsoft.Extensions.Logging
- Optional filtering to include/exclude specific queues or topics
- Rich metadata storage including dead-letter reasons and timestamps
- Graceful shutdown with proper resource disposal
- High-performance concurrent message processing

## Prerequisites

- .NET 8.0 or later
- Azure Service Bus
- Azure Storage Account

## Configuration

### appsettings.json

The `appsettings.json` file contains configuration settings for logging, allowed hosts, Azure Service Bus, and Azure Storage Account.

```json
{

  // Configuration settings for the Azure Service Bus
  "ServiceBus": {
    // Connection string for the Azure Service Bus
    "ConnectionString": "",
    
    // Optional: Include only specific queues/topics (omit to process all)
    "IncludeQueues": ["queue1", "queue2"],
    "IncludeTopics": ["topic1", "topic2"],
    
    // Optional: Exclude specific queues/topics
    "ExcludeQueues": ["system-queue"],
    "ExcludeTopics": ["system-topic"],
    
    // Performance tuning
    "PrefetchCount": 50,
    "MaxConcurrentCalls": 10
  },
  
  // Configuration settings for the Azure Storage Account
  "StorageAccount": {
    // Connection string for the Azure Storage Account
    "ConnectionString": "",
    // Name of the blob container to store dead-letter messages
    "ContainerName": "bus-dead-letters"
  }
}
```

- **Logging**: Configuration for logging levels
- **AllowedHosts**: Specifies the allowed hosts for the application
- **ServiceBus**: Configuration settings for the Azure Service Bus
  - `ConnectionString`: The connection string for the Azure Service Bus (required)
  - `IncludeQueues`: Optional array of queue names to monitor (null = all queues)
  - `IncludeTopics`: Optional array of topic names to monitor (null = all topics)
  - `ExcludeQueues`: Optional array of queue names to skip
  - `ExcludeTopics`: Optional array of topic names to skip
  - `PrefetchCount`: Number of messages to prefetch for better throughput (default: 50)
  - `MaxConcurrentCalls`: Maximum concurrent message processing calls (default: 10)
- **StorageAccount**: Configuration settings for the Azure Storage Account
  - `ConnectionString`: The connection string for the Azure Storage Account (required)
  - `ContainerName`: The name of the blob container to store dead-letter messages

## Architecture

The application consists of two background services:

### QueueCleanupService
Monitors dead-letter queues for Service Bus queues and archives messages to blob storage.

### SubscriptionCleanupService
Monitors dead-letter queues for Service Bus topic subscriptions and archives messages to blob storage.

### Key Features

- **Hierarchical Blob Naming**: Messages are stored with date-based paths for easy organization:
  - Queues: `queues/{queueName}/{yyyy/MM/dd}/{messageId}.json`
  - Topics: `topics/{topicName}/{subscriptionName}/{yyyy/MM/dd}/{messageId}.json`
  
- **Rich Metadata**: Each blob includes metadata for searchability:
  - Dead-letter reason
  - Enqueued time
  - Queue/Topic/Subscription name

- **Diagnostic Information**: Stored messages include:
  - Original message body (Base64 encoded)
  - All message headers and properties
  - Dead-letter reason and error description
  - Enqueued time and dead-letter time
  - Delivery count

- **Graceful Shutdown**: Proper resource disposal and processor cleanup on application shutdown

## Blob Storage Structure

Messages are organized in a hierarchical structure:

```
bus-dead-letters/
├── queues/
│   └── {queue-name}/
│       └── {year}/
│           └── {month}/
│               └── {day}/
│                   └── {message-id}.json
└── topics/
    └── {topic-name}/
        └── {subscription-name}/
            └── {year}/
                └── {month}/
                    └── {day}/
                        └── {message-id}.json
```

## How to Run

1. Clone the repository
2. Open the project in your preferred IDE
3. Update the `appsettings.json` file with your Azure Service Bus and Azure Storage Account connection strings
4. Optionally configure filtering and performance settings
4. Build and run the project.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.