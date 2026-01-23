# Azure Service Bus Emulator Setup

## Overview

Your Aspire project is now configured to run **Azure Service Bus Emulator** and **Azure Storage Emulator (Azurite)** for local development!

## What's Included

### 1. Azure Service Bus Emulator
- Runs as a Docker container
- Pre-configured with:
  - Queue: `deadletterqueue`
  - Topic: `testtopic`
  - Subscription: `testsubscription`

### 2. Azure Storage Emulator (Azurite)
- Runs as a Docker container
- Provides blob storage for dead-letter message archives

## How to Run

```bash
cd ServiceBusDeadLettersCleanup.AppHost
dotnet run
```

This will:
1. Pull the Azure Service Bus emulator Docker image (first time only)
2. Pull the Azurite storage emulator Docker image (first time only)
3. Start both emulators
4. Start your ServiceBus cleanup service
5. Open the Aspire Dashboard

## Aspire Dashboard

The dashboard will show:
- **servicebus** - Azure Service Bus emulator
- **storage** - Azure Storage emulator (Azurite)
- **servicebuscleanup** - Your cleanup service

You can monitor:
- Logs from all services
- Connection strings (automatically injected)
- Resource health
- Metrics and traces

## Connection Strings

Aspire automatically injects connection strings as environment variables:

- **Service Bus**: `servicebus__connectionstring`
- **Storage**: `blobs__connectionstring`

Your service will receive these automatically via the Aspire integration packages.

## Testing Locally

### Create Test Messages

You can use the Azure Service Bus emulator to:
1. Send messages to the test topic/subscription
2. Force messages into dead-letter queues
3. Test your cleanup service behavior

### Access Emulator

The Service Bus emulator connection string will be available in the Aspire Dashboard under the `servicebus` resource.

## Switching to Azure

When ready to test against real Azure resources:

1. Remove `.RunAsEmulator()` calls in [Program.cs](ServiceBusDeadLettersCleanup.AppHost/Program.cs)
2. Configure Azure credentials in user secrets:

```json
{
    "Azure": {
      "SubscriptionId": "<your subscription id>",
      "ResourceGroupPrefix": "<prefix>",
      "Location": "eastus"
    }
}
```

3. Run `dotnet user-secrets set Azure:SubscriptionId "your-id"` etc.

## Docker Requirements

Make sure Docker Desktop is running before starting the AppHost, as the emulators run in Docker containers.

## Troubleshooting

If emulators don't start:
- Check Docker is running
- Check ports aren't in use by other services
- Review logs in the Aspire Dashboard

## Additional Resources

- [Aspire Azure Service Bus docs](https://learn.microsoft.com/dotnet/aspire/storage/azure-service-bus-component)
- [Azure Service Bus Emulator](https://learn.microsoft.com/azure/service-bus-messaging/overview-emulator)
- [Azurite Storage Emulator](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)
