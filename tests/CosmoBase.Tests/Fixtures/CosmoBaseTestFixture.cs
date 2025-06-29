using System.Collections.ObjectModel;
using CosmoBase.Abstractions.Configuration;
using CosmoBase.Abstractions.Interfaces;
using CosmoBase.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace CosmoBase.Tests.Fixtures;

/// <summary>
/// Test fixture for CosmoBase integration tests with real Cosmos DB emulator
/// </summary>
public class CosmoBaseTestFixture : IAsyncLifetime, IDisposable
{
    private ServiceProvider? _serviceProvider;
    private readonly IConfiguration _configuration;
    private CosmosClient? _cosmosClient;

    public IServiceProvider ServiceProvider => _serviceProvider ??
                                               throw new InvalidOperationException(
                                                   "ServiceProvider not initialized. Call InitializeAsync first.");

    public CosmoBaseTestFixture()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            // Use app settings
            .AddJsonFile("Configuration/appsettings.json", optional: true, reloadOnChange: true)
            // Override with local settings (if you want to use your own CosmosDb in Azure, etc.)
            .AddJsonFile("localsettings.json", optional: true, reloadOnChange: true);


        _configuration = builder.Build();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await CleanupTestDataAsync();
            await _serviceProvider.DisposeAsync();
        }

        _cosmosClient?.Dispose();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _cosmosClient?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        // Add logging with console output
        services.AddLogging(builder => { builder.AddConsole(); });

        // Add configuration
        services.AddSingleton(_configuration);

        // Create test user context
        var testUserContext = new TestUserContext("TestUser");

        // Add CosmoBase with test configuration
        services.AddCosmoBase(_configuration, testUserContext);

        // Build service provider
        _serviceProvider = services.BuildServiceProvider();
        var cfg = _serviceProvider.GetRequiredService<IOptions<CosmosConfiguration>>().Value;

        var cosmosClients = _serviceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();

        // Create direct Cosmos client for setup/cleanup
        _cosmosClient = cosmosClients.Values.First();

        // Ensure test database exists
        await EnsureTestDatabaseAsync();
    }

    private async Task EnsureTestDatabaseAsync()
    {
        try
        {
            if (_cosmosClient == null) return;

            // Create test database
            var cfg = _serviceProvider!.GetRequiredService<IOptions<CosmosConfiguration>>().Value;
            var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync("CosmoBaseTestDb");

            // Create containers based on model configurations
            foreach (var modelConfig in cfg.CosmosModelConfigurations)
            {
                var partitionKeyPath = $"/{modelConfig.PartitionKey}";
                await database.Database.CreateContainerIfNotExistsAsync(
                    modelConfig.CollectionName,
                    partitionKeyPath);
            }

            var logger = _serviceProvider?.GetService<ILogger<CosmoBaseTestFixture>>();
            logger?.LogInformation("Test database and containers created successfully");
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
        if (bool.Parse(cleanupEnabled ?? "true"))
        {
            try
            {
                if (_cosmosClient == null) return;

                // Delete test database (this removes all data)
                await _cosmosClient.GetDatabase("CosmoBaseTestDb").DeleteAsync();

                var logger = _serviceProvider?.GetService<ILogger<CosmoBaseTestFixture>>();
                logger?.LogInformation("Test database cleaned up successfully");
            }
            catch (Exception ex)
            {
                // Log cleanup errors but don't fail the test
                var logger = _serviceProvider?.GetService<ILogger<CosmoBaseTestFixture>>();
                logger?.LogWarning(ex, "Failed to cleanup test data");
            }
        }
    }

    public T GetRequiredService<T>() where T : class
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    public T? GetService<T>()
    {
        return ServiceProvider.GetService<T>();
    }

    /// <summary>
    /// Get a fresh test database name for isolated tests
    /// </summary>
    public string GetTestDatabaseName() => $"CosmoBaseTestDb_{Guid.NewGuid():N}";
}

/// <summary>
/// Test user context for audit field testing
/// </summary>
public class TestUserContext(string userId) : IUserContext
{
    public string? GetCurrentUser() => userId;
}

/// <summary>
/// XUnit logger provider for test output - use per-test method
/// </summary>
public class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _testOutputHelper;

    public XunitLoggerProvider(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(_testOutputHelper, categoryName);
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// XUnit logger implementation - use per-test method
/// </summary>
public class XunitLogger : ILogger
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper testOutputHelper, string categoryName)
    {
        _testOutputHelper = testOutputHelper;
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NoOpDisposable();

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        try
        {
            var message = formatter(state, exception);
            _testOutputHelper.WriteLine($"[{logLevel}] {_categoryName}: {message}");

            if (exception != null)
            {
                _testOutputHelper.WriteLine($"Exception: {exception}");
            }
        }
        catch
        {
            // Ignore logging failures in tests
        }
    }

    private class NoOpDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}