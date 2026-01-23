using ServiceBusDeadLettersCleanup.ServiceBus;
using ServiceBusDeadLettersCleanup.ServiceBus.Configs;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddConfigs();
builder.Services
    .AddHostedService<SubscriptionCleanupService>()
    .AddHostedService<QueueCleanupService>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();