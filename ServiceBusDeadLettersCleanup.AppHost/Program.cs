var builder = DistributedApplication.CreateBuilder(args);

var serviceBusCleanup = builder.AddProject<Projects.ServiceBusDeadLettersCleanup>("servicebusdeadletterscleanup");

builder.Build().Run();
