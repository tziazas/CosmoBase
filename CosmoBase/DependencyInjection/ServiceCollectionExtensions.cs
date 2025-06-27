using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using CosmoBase.Abstractions.Configuration;
using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Configuration; // for CosmosConfigurationValidator
using CosmoBase.Mapping;
using CosmoBase.Repositories;
using CosmoBase.DataServices;
using CosmoBase.Services;
using CosmoBase.Validation;
using Microsoft.Azure.Cosmos;

namespace CosmoBase.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private const string DefaultSectionName = "CosmoBase";

    /// <summary>
    /// Adds CosmoBase services to the specified <see cref="IServiceCollection"/> using the default "CosmoBase" configuration section.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The configuration instance to bind from.</param>
    /// <param name="userContext">The user context implementation for audit field tracking. Use SystemUserContext for background services.</param>
    /// <param name="configureOptions">An optional action to configure the <see cref="CosmosConfiguration"/> options.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userContext"/> is null.</exception>
    public static IServiceCollection AddCosmoBase(
        this IServiceCollection services,
        IConfiguration configuration,
        IUserContext userContext,
        Action<CosmosConfiguration>? configureOptions = null)
    {
        if (userContext == null) throw new ArgumentNullException(nameof(userContext));
        return services.AddCosmoBase(configuration.GetSection(DefaultSectionName), userContext, configureOptions);
    }

    /// <summary>
    /// Adds CosmoBase services to the specified <see cref="IServiceCollection"/> using the provided configuration section.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="section">The configuration section to bind <see cref="CosmosConfiguration"/> from.</param>
    /// <param name="userContext">The user context implementation for audit field tracking. Use SystemUserContext for background services.</param>
    /// <param name="configureOptions">An optional action to configure the <see cref="CosmosConfiguration"/> options after binding.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userContext"/> is null.</exception>
    public static IServiceCollection AddCosmoBase(
        this IServiceCollection services,
        IConfigurationSection section,
        IUserContext userContext,
        Action<CosmosConfiguration>? configureOptions = null)
    {
        if (userContext == null) throw new ArgumentNullException(nameof(userContext));

        // Start the options builder
        var optionsBuilder = services
            .AddOptions<CosmosConfiguration>()
            .Bind(section)
            .ValidateDataAnnotations();

        // Only call PostConfigure if the caller actually passed a lambda
        if (configureOptions != null)
        {
            optionsBuilder.PostConfigure(configureOptions);
        }

        // Fail fast on startup
        optionsBuilder.ValidateOnStart();

        // Register validator
        services.AddSingleton<IValidateOptions<CosmosConfiguration>, CosmosConfigurationValidator>();

        // Register the provided user context
        services.AddSingleton(userContext);

        // Wire up base services using IOptions<CosmosConfiguration>
        return services.AddCosmoBaseInternal();
    }

    /// <summary>
    /// Adds CosmoBase services to the specified <see cref="IServiceCollection"/> using a configuration action.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">An action to configure the <see cref="CosmosConfiguration"/> options.</param>
    /// <param name="userContext">The user context implementation for audit field tracking. Use SystemUserContext for background services.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configureOptions"/> or <paramref name="userContext"/> is null.</exception>
    public static IServiceCollection AddCosmoBase(
        this IServiceCollection services,
        Action<CosmosConfiguration> configureOptions,
        IUserContext userContext)
    {
        if (configureOptions == null) throw new ArgumentNullException(nameof(configureOptions));
        if (userContext == null) throw new ArgumentNullException(nameof(userContext));

        // Configure options programmatically
        services.AddOptions<CosmosConfiguration>()
            .Configure(configureOptions)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<CosmosConfiguration>, CosmosConfigurationValidator>();

        // Register the provided user context
        services.AddSingleton(userContext);

        return services.AddCosmoBaseInternal();
    }

    /// <summary>
    /// Registers core CosmoBase services including repositories, data services, and utilities.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    private static IServiceCollection AddCosmoBaseInternal(this IServiceCollection services)
    {
        // A) CosmosConfiguration for injection if needed
        services.AddSingleton(sp =>
            sp.GetRequiredService<IOptions<CosmosConfiguration>>().Value
        );

        // B) Named CosmosClient dictionary
        services.AddSingleton<IReadOnlyDictionary<string, CosmosClient>>(sp =>
        {
            var cfg = sp.GetRequiredService<IOptions<CosmosConfiguration>>().Value;

            return cfg.CosmosClientConfigurations
                .ToDictionary(
                    c => c.Name,
                    c => new CosmosClient(
                        c.ConnectionString,
                        new CosmosClientOptions
                        {
                            AllowBulkExecution = c.AllowBulkExecution ?? true,
                            ConnectionMode = ParseConnectionMode(c.ConnectionMode) ?? ConnectionMode.Direct,
                            MaxRetryAttemptsOnRateLimitedRequests = c.MaxRetryAttempts ?? 9,
                            MaxRetryWaitTimeOnRateLimitedRequests =
                                TimeSpan.FromSeconds(c.MaxRetryWaitTimeInSeconds ?? 30)
                        })
                );
        });

        // C) Expose the raw model mappings
        services.AddSingleton(sp =>
            sp.GetRequiredService<IOptions<CosmosConfiguration>>()
                .Value
                .CosmosModelConfigurations
        );

        // D) Core utilities
        services.TryAddSingleton(typeof(IItemMapper<,>), typeof(DefaultItemMapper<,>));
        services.TryAddSingleton(_ =>
            Policy.Handle<CosmosException>()
                .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(Math.Pow(2, i)))
        );

        // E) Add memory cache for count caching
        services.TryAddSingleton<IMemoryCache, MemoryCache>();

        // F) Register audit field manager (uses the registered IUserContext)
        services.TryAddSingleton(typeof(IAuditFieldManager<>), typeof(AuditFieldManager<>));

        // G) Register default validators for all document types
        services.TryAddSingleton(typeof(ICosmosValidator<>), typeof(CosmosValidator<>));

        // H) Repositories & DataServices
        services.TryAddScoped(typeof(ICosmosRepository<>), typeof(CosmosRepository<>));
        services.TryAddScoped(typeof(IDataReadService<,>), typeof(CosmosDataReadService<,>));
        services.TryAddScoped(typeof(IDataWriteService<,>), typeof(CosmosDataWriteService<,>));

        return services;
    }

    /// <summary>
    /// Parses a connection mode string to the Cosmos ConnectionMode enum.
    /// </summary>
    /// <param name="connectionMode">The connection mode string ("Direct" or "Gateway").</param>
    /// <returns>The parsed ConnectionMode or null if invalid/empty.</returns>
    private static ConnectionMode? ParseConnectionMode(string? connectionMode)
    {
        if (string.IsNullOrWhiteSpace(connectionMode))
            return null;

        return connectionMode.ToUpperInvariant() switch
        {
            "DIRECT" => ConnectionMode.Direct,
            "GATEWAY" => ConnectionMode.Gateway,
            _ => null
        };
    }
}