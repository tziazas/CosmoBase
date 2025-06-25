using CosmoBase.Abstractions.Configuration;
using CosmoBase.Abstractions.Exceptions;
using CosmoBase.Abstractions.Interfaces;
using CosmoBase.DataServices;
using CosmoBase.Mapping;
using CosmoBase.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace CosmoBase.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Bind CosmosConfiguration from the given section, then allow 
    /// further overrides in code, and register all named CosmosClients.
    /// </summary>
    public static IServiceCollection AddCosmoBase(
        this IServiceCollection services,
        IConfigurationSection section,
        Action<CosmosConfiguration>? configureOptions = null)
    {
        // 1) Bind the JSON section
        var config = section.Get<CosmosConfiguration>()
                     ?? new CosmosConfiguration();

        // 2) Let caller tweak it
        configureOptions?.Invoke(config);

        // 3) Delegate to the shared registration logic
        return services.AddCosmoBaseInternal(config);
    }

    /// <summary>
    /// Skip IConfiguration entirely—build the config purely in code.
    /// </summary>
    public static IServiceCollection AddCosmoBase(
        this IServiceCollection services,
        Action<CosmosConfiguration> configureOptions)
    {
        var config = new CosmosConfiguration();
        configureOptions(config);
        return services.AddCosmoBaseInternal(config);
    }

    /// <summary>
    /// Shared logic: register the config object & build your CosmosClient map.
    /// </summary>
    private static IServiceCollection AddCosmoBaseInternal(
        this IServiceCollection services,
        CosmosConfiguration config)
    {
        // A) register the raw config for direct injection if needed
        services.AddSingleton(config);

        // B) build and register all named CosmosClient instances
        services.AddSingleton<IReadOnlyDictionary<string, CosmosClient>>(_ =>
            config
                .CosmosClientConfigurations
                .ToDictionary(
                    c => c.Name,
                    c => new CosmosClient(
                        c.ConnectionString,
                        new CosmosClientOptions
                        {
                            AllowBulkExecution = true,
                            // you could also wire in c.NumberOfWorkers here
                        })
                )
        );

        // C) register the model‐configuration list
        services.AddSingleton(config.CosmosModelConfigurations);

        // D) common dependencies: mapper + retry policy
        services.AddSingleton(typeof(IItemMapper<,>), typeof(DefaultItemMapper<,>));
        services.AddSingleton(_ =>
            Policy
                .Handle<CosmosBaseException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: i => TimeSpan.FromSeconds(Math.Pow(2, i))
                )
        );

        // E) Register all repositories
        services.AddScoped(
            typeof(ICosmosRepository<>),
            typeof(CosmosRepository<>)
        );

        // F) Register all Data Services
        services.AddScoped(typeof(IDataReadService<>), typeof(CosmosDataReadService<,>));
        services.AddScoped(typeof(IDataWriteService<>), typeof(CosmosDataWriteService<,>));

        return services;
    }
}