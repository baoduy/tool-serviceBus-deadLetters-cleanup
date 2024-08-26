using System.ComponentModel.DataAnnotations;

namespace ServiceBusDeadLettersCleanup.ServiceBus.Configs;

public sealed class BusConfig
{
    public static string Name => "ServiceBus";

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = default!;
}

public sealed class StorageConfig
{
    public static string Name => "StorageAccount";

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = default!;

    [Required(AllowEmptyStrings = false)]
    public string ContainerName { get; set; } = default!;
}

public static class Configs
{
    public static IServiceCollection AddConfigs(this IServiceCollection services)
    {
        services.AddOptions<BusConfig>()
            .BindConfiguration(BusConfig.Name)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<StorageConfig>()
            .BindConfiguration(StorageConfig.Name)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}