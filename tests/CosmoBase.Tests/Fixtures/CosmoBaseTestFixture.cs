using System.Text.Json;
using System.Text.Json.Serialization;
using CosmoBase.Abstractions.Configuration;
using CosmoBase.Abstractions.Interfaces;
using CosmoBase.DependencyInjection;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.CosmosDb;
using Xunit.Abstractions;

namespace CosmoBase.Tests.Fixtures;

/// <summary>
/// Shared test fixture for CosmoBase integration tests.
///
/// Connection strategy (in priority order):
///   1. localsettings.json present → use its connection string (local emulator or cloud Cosmos).
///      This file is gitignored and only exists on a developer's machine by choice.
///   2. No localsettings.json → spin up a Cosmos emulator via Testcontainers (Docker required).
///      This is the path taken on GitHub Actions and on fresh clones.
/// </summary>
public class CosmoBaseTestFixture : IAsyncLifetime, IDisposable
{
    private ServiceProvider? _serviceProvider;
    private readonly IConfiguration _configuration;
    private CosmosClient? _cosmosClient;
    private CosmosDbContainer? _container;

    public IServiceProvider ServiceProvider => _serviceProvider ??
                                               throw new InvalidOperationException(
                                                   "ServiceProvider not initialized. Call InitializeAsync first.");

    public CosmoBaseTestFixture()
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("Configuration/appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("localsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    public async Task InitializeAsync()
    {
        string connectionString;

        var localSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "localsettings.json");
        var useTestcontainers = !File.Exists(localSettingsPath);

        if (useTestcontainers)
        {
            // No local override — start the Cosmos emulator in a container.
            // Works on GitHub Actions (Docker available on ubuntu-latest) and locally with Docker Desktop.
            _container = new CosmosDbBuilder().Build();
            await _container.StartAsync();
            connectionString = _container.GetConnectionString();
        }
        else
        {
            connectionString = _configuration["CosmoBase:CosmosClientConfigurations:0:ConnectionString"]
                ?? throw new InvalidOperationException(
                    "localsettings.json is present but has no connection string at " +
                    "CosmoBase:CosmosClientConfigurations:0:ConnectionString.");
        }

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddSingleton(_configuration);

        // Register CosmoBase using config for model/logging settings, but override
        // the connection string with the runtime value resolved above.
        services.AddCosmoBase(_configuration, new TestUserContext("TestUser"), options =>
        {
            options.CosmosClientConfigurations = options.CosmosClientConfigurations
                .Select(c => c with { ConnectionString = connectionString })
                .ToList();
        });

        if (useTestcontainers)
        {
            // The Cosmos emulator in Testcontainers uses a self-signed TLS certificate.
            // Override the CosmosClient dictionary registered by AddCosmoBase with a
            // client configured to accept it. In .NET DI the last registration wins for
            // single-service resolution, so this replaces the internal registration.
            var clientName = _configuration["CosmoBase:CosmosClientConfigurations:0:Name"] ?? "TestPrimary";

            services.AddSingleton<IReadOnlyDictionary<string, CosmosClient>>(_ =>
                new Dictionary<string, CosmosClient>
                {
                    [clientName] = new CosmosClient(
                        connectionString,
                        new CosmosClientOptions
                        {
                            // Accept the emulator's self-signed certificate — test use only.
                            HttpClientFactory = () => new HttpClient(
                                new HttpClientHandler
                                {
                                    ServerCertificateCustomValidationCallback =
                                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                                }),
                            ConnectionMode = ConnectionMode.Gateway,
                            AllowBulkExecution = true,
                            MaxRetryAttemptsOnRateLimitedRequests = 3,
                            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(10),
                            UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions
                            {
                                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                            }
                        })
                });
        }

        _serviceProvider = services.BuildServiceProvider();

        var cosmosClients = _serviceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        _cosmosClient = cosmosClients.Values.First();

        await EnsureTestDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await CleanupTestDataAsync();
            await _serviceProvider.DisposeAsync();
        }

        _cosmosClient?.Dispose();

        if (_container != null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _cosmosClient?.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task EnsureTestDatabaseAsync()
    {
        try
        {
            if (_cosmosClient == null) return;

            var cfg = _serviceProvider!.GetRequiredService<IOptions<CosmosConfiguration>>().Value;
            var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync("CosmoBaseTestDb");

            foreach (var modelConfig in cfg.CosmosModelConfigurations)
            {
                await database.Database.CreateContainerIfNotExistsAsync(
                    modelConfig.CollectionName,
                    $"/{modelConfig.PartitionKey}");
            }

            var logger = _serviceProvider.GetService<ILogger<CosmoBaseTestFixture>>();
            logger?.LogInformation("Test database and containers ready");
        }
        catch (Exception ex)
        {
            var logger = _serviceProvider?.GetService<ILogger<CosmoBaseTestFixture>>();
            logger?.LogError(ex, "Failed to create test database");
            throw;
        }
    }

    private async Task CleanupTestDataAsync()
    {
        var cleanupEnabled = _configuration.GetSection("TestSettings")["DatabaseCleanupEnabled"];
        if (!bool.TryParse(cleanupEnabled, out var enabled) || enabled)
        {
            try
            {
                if (_cosmosClient == null) return;
                await _cosmosClient.GetDatabase("CosmoBaseTestDb").DeleteAsync();

                var logger = _serviceProvider?.GetService<ILogger<CosmoBaseTestFixture>>();
                logger?.LogInformation("Test database cleaned up");
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider?.GetService<ILogger<CosmoBaseTestFixture>>();
                logger?.LogWarning(ex, "Failed to cleanup test data (non-fatal)");
            }
        }
    }

    public T GetRequiredService<T>() where T : class => ServiceProvider.GetRequiredService<T>();
    public T? GetService<T>() => ServiceProvider.GetService<T>();
    public string GetTestDatabaseName() => $"CosmoBaseTestDb_{Guid.NewGuid():N}";
}

/// <summary>Test user context for audit field verification.</summary>
public class TestUserContext(string userId) : IUserContext
{
    public string? GetCurrentUser() => userId;
}

/// <summary>Routes xUnit test output to ILogger.</summary>
public class XunitLoggerProvider(ITestOutputHelper output) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new XunitLogger(output, categoryName);
    public void Dispose() { }
}

public class XunitLogger(ITestOutputHelper output, string category) : ILogger
{
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NoOpDisposable();
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        try
        {
            output.WriteLine($"[{logLevel}] {category}: {formatter(state, exception)}");
            if (exception != null)
                output.WriteLine($"Exception: {exception}");
        }
        catch
        {
            // Swallow: xUnit throws if output is written after the test completes
        }
    }

    private sealed class NoOpDisposable : IDisposable { public void Dispose() { } }
}
