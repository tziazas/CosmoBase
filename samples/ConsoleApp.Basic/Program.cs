using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ConsoleApp.Basic.Services;
using CosmoBase.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace ConsoleApp.Basic;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 CosmoBase Console App - Basic CRUD Operations");
        Console.WriteLine("================================================");

        // Build host with services (simpler approach)
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                // Add local.settings.json for local development overrides
                config.AddJsonFile("localsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration is automatically loaded by Host.CreateDefaultBuilder
                var configuration = context.Configuration;

                // Add logging
                services.AddLogging(builder => builder.AddConsole());
                
                // Add CosmoBase with system user context for console apps
                services.AddCosmoBaseWithSystemUser(
                    configuration,
                    "ConsoleApp.Basic", // System user name for audit fields
                    config =>
                    {
                        // Optional: Override settings programmatically
                        Console.WriteLine($"Configured {config.CosmosClientConfigurations.Count} Cosmos clients");
                        Console.WriteLine($"Configured {config.CosmosModelConfigurations.Count} models");
                    });

                // Add application services
                services.AddTransient<ProductService>();
                services.AddTransient<OrderService>();
                services.AddTransient<DemoRunner>();
            })
            .Build();

        try
        {
            // CREATE DATABASE AND CONTAINERS BEFORE RUNNING DEMO
            await EnsureDatabaseExistsAsync(host.Services);
            
            // Run the demo
            var demoRunner = host.Services.GetRequiredService<DemoRunner>();
            await demoRunner.RunAsync();

            Console.WriteLine("\n✅ Demo completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Demo failed: {ex.Message}");
            Console.WriteLine($"Details: {ex}");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
    
    private static async Task EnsureDatabaseExistsAsync(IServiceProvider services)
    {
        Console.WriteLine("🔧 Setting up database and containers...");
    
        var cosmosClients = services.GetRequiredService<IReadOnlyDictionary<string, Microsoft.Azure.Cosmos.CosmosClient>>();
        var config = services.GetRequiredService<CosmoBase.Abstractions.Configuration.CosmosConfiguration>();
    
        var client = cosmosClients.Values.First();
    
        // Create database
        var database = await client.CreateDatabaseIfNotExistsAsync("SampleAppDb");
        Console.WriteLine("✅ Database 'SampleAppDb' ready");
    
        // Create containers based on your configuration
        foreach (var modelConfig in config.CosmosModelConfigurations)
        {
            var partitionKeyPath = $"/{modelConfig.PartitionKey}";
            await database.Database.CreateContainerIfNotExistsAsync(
                modelConfig.CollectionName,
                partitionKeyPath);
        
            Console.WriteLine($"✅ Container '{modelConfig.CollectionName}' ready (partition: {partitionKeyPath})");
        }
    }
}

public class DemoRunner
{
    private readonly ProductService _productService;
    private readonly OrderService _orderService;
    private readonly ILogger<DemoRunner> _logger;

    public DemoRunner(
        ProductService productService,
        OrderService orderService,
        ILogger<DemoRunner> logger)
    {
        _productService = productService;
        _orderService = orderService;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("\n📦 Starting Product Operations...");
        await _productService.DemonstrateProductOperationsAsync();

        Console.WriteLine("\n🛒 Starting Order Operations...");
        await _orderService.DemonstrateOrderOperationsAsync();

        Console.WriteLine("\n⚡ Starting Bulk Operations...");
        await _productService.DemonstrateBulkOperationsAsync();

        Console.WriteLine("\n📊 Starting Query & Count Operations...");
        await _productService.DemonstrateQueryOperationsAsync();

        Console.WriteLine("\n🗑️ Starting Delete Operations...");
        await _productService.DemonstrateDeleteOperationsAsync();
    }
}