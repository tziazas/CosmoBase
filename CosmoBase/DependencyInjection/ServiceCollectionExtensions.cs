using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using CosmoBase.Abstractions.Configuration;
using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Configuration;         // for CosmosConfigurationValidator
using CosmoBase.Mapping;
using CosmoBase.Repositories;
using CosmoBase.DataServices;
using Microsoft.Azure.Cosmos;

namespace CosmoBase.DependencyInjection;

public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCosmoBase(
            this IServiceCollection services,
            IConfigurationSection section,
            Action<CosmosConfiguration>? configureOptions = null)
        {
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

            // 2) Register validator
            services.AddSingleton<IValidateOptions<CosmosConfiguration>, CosmosConfigurationValidator>();

            // 3) Wire up base services using IOptions<CosmosConfiguration>
            return services.AddCosmoBaseInternal();
        }

        public static IServiceCollection AddCosmoBase(
            this IServiceCollection services,
            Action<CosmosConfiguration> configureOptions)
        {
            // same as above, but skipping JSON section
            services.AddOptions<CosmosConfiguration>()
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<CosmosConfiguration>, CosmosConfigurationValidator>();

            return services.AddCosmoBaseInternal();
        }

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
                               new CosmosClientOptions { AllowBulkExecution = true })
                           );
            });

            // C) Expose the raw model mappings
            services.AddSingleton(sp =>
                sp.GetRequiredService<IOptions<CosmosConfiguration>>()
                  .Value
                  .CosmosModelConfigurations
            );

            // D) Core utilities
            services.AddSingleton(typeof(IItemMapper<,>), typeof(DefaultItemMapper<,>));
            services.AddSingleton(_ =>
                Policy.Handle<CosmosException>()
                      .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(Math.Pow(2, i)))
            );

            // E) Repositories & DataServices
            services.AddScoped(typeof(ICosmosRepository<>), typeof(CosmosRepository<>));
            services.AddScoped(typeof(IDataReadService<>), typeof(CosmosDataReadService<,>));
            services.AddScoped(typeof(IDataWriteService<>), typeof(CosmosDataWriteService<,>));

            return services;
        }
    }