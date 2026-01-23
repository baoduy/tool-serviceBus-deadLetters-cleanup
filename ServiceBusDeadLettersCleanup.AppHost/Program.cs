var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Service Bus emulator for local development
var serviceBus = builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator(emulator =>
    {
        // Configure emulator with custom settings if needed
        emulator.WithImageTag("latest");
    });

// Add queues that your service will monitor
serviceBus.AddServiceBusQueue("deadletterqueue");

// Add topics and subscriptions for testing
var testTopic = serviceBus.AddServiceBusTopic("testtopic");
testTopic.AddServiceBusSubscription("testsubscription");

// Add Azure Blob Storage emulator (Azurite) for storing dead-letter messages
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator()
    .AddBlobs("blobs");

var serviceBusCleanup = builder.AddProject<Projects.ServiceBusDeadLettersCleanup>("servicebuscleanup")
    .WithReference(serviceBus)
    .WithReference(storage);

builder.Build().Run();
