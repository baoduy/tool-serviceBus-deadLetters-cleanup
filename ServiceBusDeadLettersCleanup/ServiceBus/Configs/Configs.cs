namespace ServiceBusDeadLettersCleanup.ServiceBus.Configs;

public class BusConfig
{
    public static string Name => "ServiceBus";
    public string ConnectionString { get; set; } = default!;
}