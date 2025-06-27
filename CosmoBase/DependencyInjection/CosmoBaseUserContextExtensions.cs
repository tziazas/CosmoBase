using CosmoBase.Abstractions.Configuration;
using CosmoBase.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CosmoBase.DependencyInjection;

public static class CosmoBaseUserContextExtensions
{
    /// <summary>
    /// Adds CosmoBase services with a system user context (recommended for background services, console apps).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The configuration instance to bind from.</param>
    /// <param name="systemUserName">The name to use for system operations (default: "System").</param>
    /// <param name="configureOptions">An optional action to configure the <see cref="CosmosConfiguration"/> options.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddCosmoBaseWithSystemUser(
        this IServiceCollection services,
        IConfiguration configuration,
        string systemUserName = "System",
        Action<CosmosConfiguration>? configureOptions = null)
    {
        return services.AddCosmoBase(
            configuration, 
            new SystemUserContext(systemUserName), 
            configureOptions);
    }

    /// <summary>
    /// Adds CosmoBase services with a system user context using programmatic configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">An action to configure the <see cref="CosmosConfiguration"/> options.</param>
    /// <param name="systemUserName">The name to use for system operations (default: "System").</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddCosmoBaseWithSystemUser(
        this IServiceCollection services,
        Action<CosmosConfiguration> configureOptions,
        string systemUserName = "System")
    {
        return services.AddCosmoBase(
            configureOptions, 
            new SystemUserContext(systemUserName));
    }

    /// <summary>
    /// Adds CosmoBase services with a custom user context using a delegate function.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The configuration instance to bind from.</param>
    /// <param name="userProvider">A function that returns the current user identifier.</param>
    /// <param name="configureOptions">An optional action to configure the <see cref="CosmosConfiguration"/> options.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddCosmoBaseWithUserProvider(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<string?> userProvider,
        Action<CosmosConfiguration>? configureOptions = null)
    {
        if (userProvider == null) throw new ArgumentNullException(nameof(userProvider));
        
        return services.AddCosmoBase(
            configuration, 
            new DelegateUserContext(userProvider), 
            configureOptions);
    }

    /// <summary>
    /// Adds CosmoBase services with a custom user context using a delegate function and programmatic configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">An action to configure the <see cref="CosmosConfiguration"/> options.</param>
    /// <param name="userProvider">A function that returns the current user identifier.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddCosmoBaseWithUserProvider(
        this IServiceCollection services,
        Action<CosmosConfiguration> configureOptions,
        Func<string?> userProvider)
    {
        if (userProvider == null) throw new ArgumentNullException(nameof(userProvider));
        
        return services.AddCosmoBase(
            configureOptions, 
            new DelegateUserContext(userProvider));
    }
}