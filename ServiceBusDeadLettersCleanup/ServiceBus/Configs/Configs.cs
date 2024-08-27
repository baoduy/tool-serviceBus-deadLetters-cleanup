using System.ComponentModel.DataAnnotations;

namespace ServiceBusDeadLettersCleanup.ServiceBus.Configs;

/// <summary>
/// Configuration settings for the Service Bus.
/// </summary>
public sealed class BusConfig
{
    // The name of the configuration section in the appsettings.json file
    public static string Name => "ServiceBus";

    // The connection string for the Service Bus, required and cannot be empty
    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = default!;
}

/// <summary>
/// Configuration settings for the Storage Account.
/// </summary>
public sealed class StorageConfig
{
    // The name of the configuration section in the appsettings.json file
    public static string Name => "StorageAccount";

    // The connection string for the Storage Account, required and cannot be empty
    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = default!;

    // The name of the blob container, required and cannot be empty
    [Required(AllowEmptyStrings = false)]
    public string ContainerName { get; set; } = default!;
}

/// <summary>
/// Static class to add configuration settings to the service collection.
/// </summary>
public static class Configs
{
    /// <summary>
    /// Extension method to add configuration settings to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the configurations to.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddConfigs(this IServiceCollection services)
    {
        // Add and bind the BusConfig settings from the configuration
        services.AddOptions<BusConfig>()
            .BindConfiguration(BusConfig.Name)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Add and bind the StorageConfig settings from the configuration
        services.AddOptions<StorageConfig>()
            .BindConfiguration(StorageConfig.Name)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}