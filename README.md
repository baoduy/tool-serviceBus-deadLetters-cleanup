# ServiceBusDeadLettersCleanup

## Overview

ServiceBusDeadLettersCleanup is a background service designed to listen to dead-letter queues of Azure Service Bus topics and write the dead-letter messages to Azure Blob Storage. This ensures that messages that cannot be processed are stored for later analysis or reprocessing.

## Features

- Listens to dead-letter queues of Azure Service Bus topics.
- Writes dead-letter messages to Azure Blob Storage.
- Configurable via `appsettings.json`.

## Prerequisites

- .NET 6.0 or later
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
    "ConnectionString": ""
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

- **Logging**: Configuration for logging levels.
- **AllowedHosts**: Specifies the allowed hosts for the application.
- **ServiceBus**: Configuration settings for the Azure Service Bus.
  - `ConnectionString`: The connection string for the Azure Service Bus.
- **StorageAccount**: Configuration settings for the Azure Storage Account.
  - `ConnectionString`: The connection string for the Azure Storage Account.
  - `ContainerName`: The name of the blob container to store dead-letter messages.

## ServiceBusBackgroundService

The `ServiceBusBackgroundService` class is a background service that listens to dead-letter queues of Azure Service Bus topics and writes the dead-letter messages to Azure Blob Storage.

### Key Methods

- `StartListeningToDeadLetterQueueAsync`: Starts listening to the dead-letter queue of a specific topic and subscription.
- `WriteMessageToBlobAsync`: Writes a dead-letter message to Azure Blob Storage.
- `ExecuteAsync`: Main execution method for the background service. It ensures the blob container exists and retrieves all topics and subscriptions from the Service Bus to start listening to their dead-letter queues.
- `StopAsync`: Method to stop the background service and dispose of the Service Bus client.

## How to Run

1. Clone the repository.
2. Open the project in your preferred IDE.
3. Update the `appsettings.json` file with your Azure Service Bus and Azure Storage Account connection strings.
4. Build and run the project.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.