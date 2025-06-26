# CosmoBase

[![Build Status](https://github.com/tziazas/CosmoBase/actions/workflows/publish.yml/badge.svg)](https://github.com/tziazas/CosmoBase/actions)
[![NuGet](https://img.shields.io/nuget/v/CosmoBase.CosmosDb.svg)](https://www.nuget.org/packages/CosmoBase)
[![License](https://img.shields.io/github/license/tziazas/CosmoBase.svg)](LICENSE)

**CosmoBase** – Enterprise-grade Azure Cosmos DB library with advanced caching, validation, bulk operations, and intelligent soft-delete handling.

---

## 🏆 Features

### **Core Capabilities**
- **Named read/write clients**: Configure multiple Cosmos endpoints (primary, replicas, emulator, etc.)
- **Per-model routing**: Route reads and writes to different endpoints via configuration
- **High-performance bulk operations**: Parallel upsert/insert with comprehensive error handling
- **Intelligent caching**: Age-based count caching with automatic invalidation
- **Advanced validation**: Comprehensive document and parameter validation with extensible rules

### **Query & Paging**
- **Continuation-token paging**: Efficient, server-side paging without re-scanning
- **LINQ & SQL queries**: Expression-based or raw SQL, streamed as `IAsyncEnumerable<T>`
- **Flexible filtering**: Array property queries and dynamic property comparisons
- **Soft-delete awareness**: Consistent filtering across all query methods

### **Enterprise Features**
- **Audit trails**: Built-in `CreatedOnUtc`, `UpdatedOnUtc`, `CreatedBy`, `UpdatedBy` fields
- **Soft-delete support**: Configurable soft-delete with `includeDeleted` parameters
- **Comprehensive retry policies**: Polly-based retry with exponential backoff
- **Metrics & observability**: Built-in telemetry and performance monitoring
- **DTO ↔ DAO mapping**: Zero-dependency default mapper or bring your own (AutoMapper, Mapster)

### **Developer Experience**
- **Resource management**: Automatic disposal of Cosmos clients and resources
- **Extensive validation**: Early detection of configuration and data issues
- **Rich error handling**: Detailed error messages with context and suggestions
- **Type safety**: Strong typing throughout with compile-time validation

---

## 🚀 Installation

From the command line:

```bash
dotnet add package CosmoBase
```

Or via NuGet Package Manager in Visual Studio:

```
Install-Package CosmoBase
```

---

## 📖 Quickstart

### 1. Add your configuration

In `appsettings.json`:

```jsonc
{
  "CosmoBase": {
    "CosmosClientConfigurations": [
      {
        "Name": "Primary",
        "ConnectionString": "AccountEndpoint=https://myaccount.documents.azure.com:443/;AccountKey=mykey==;",
        "NumberOfWorkers": 10,
        "AllowBulkExecution": true,
        "ConnectionMode": "Direct",
        "MaxRetryAttempts": 5,
        "MaxRetryWaitTimeInSeconds": 30
      },
      {
        "Name": "ReadReplica",
        "ConnectionString": "AccountEndpoint=https://myaccount-eastus.documents.azure.com:443/;AccountKey=mykey2==;",
        "NumberOfWorkers": 5,
        "AllowBulkExecution": false,
        "ConnectionMode": "Direct",
        "MaxRetryAttempts": 3,
        "MaxRetryWaitTimeInSeconds": 15
      }
    ],
    "CosmosModelConfigurations": [
      {
        "ModelName": "Product",
        "DatabaseName": "ProductCatalog",
        "CollectionName": "Products",
        "PartitionKey": "/category",
        "ReadCosmosClientConfigurationName": "ReadReplica",
        "WriteCosmosClientConfigurationName": "Primary"
      },
      {
        "ModelName": "Order",
        "DatabaseName": "OrderManagement",
        "CollectionName": "Orders",
        "PartitionKey": "/customerId",
        "ReadCosmosClientConfigurationName": "Primary",
        "WriteCosmosClientConfigurationName": "Primary"
      }
    ]
  }
}
```

### 2. Register CosmoBase and Data Services in DI

In `Program.cs`:

```csharp
using CosmoBase.DependencyInjection;
using CosmoBase.Abstractions.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Registers Cosmos clients, repositories, validators, and data services
builder.Services.AddCosmoBase(
    builder.Configuration.GetSection("CosmoBase"),
    config =>
    {
        // Optional: Override specific settings
        config.CosmosClientConfigurations
              .First(c => c.Name == "Primary")
              .NumberOfWorkers = 12;
    });

// Optional: Register custom validators for specific types
builder.Services.AddSingleton<ICosmosValidator<Product>, CustomProductValidator>();

var app = builder.Build();
```

### 3. Use Data Services (Recommended)

**High-level data services** provide the best developer experience:

```csharp
public class ProductService
{
    private readonly IDataReadService<Product>  _reader;
    private readonly IDataWriteService<Product> _writer;

    public ProductService(
        IDataReadService<Product> reader,
        IDataWriteService<Product> writer)
    {
        _reader = reader;
        _writer = writer;
    }

    public async Task ProcessProductsAsync()
    {
        // Bulk save with automatic validation and retry
        var newProducts = GetNewProductDtos();
        await _writer.SaveAsync(newProducts);

        // Stream products with intelligent caching
        await foreach (var product in _reader.GetAllAsyncEnumerable(
            cancellationToken: CancellationToken.None,
            limit: 100, offset: 0, count: 500))
        {
            await ProcessProduct(product);
        }

        // Get cached count (15-minute cache)
        var totalCount = await _reader.GetCountAsync("electronics", cacheMinutes: 15);
    }
}
```

### 4. Use Repository Directly (Advanced)

For advanced scenarios requiring more control:

```csharp
public class AdvancedProductService
{
    private readonly ICosmosRepository<ProductDocument> _repository;

    public AdvancedProductService(ICosmosRepository<ProductDocument> repository)
    {
        _repository = repository;
    }

    public async Task AdvancedOperationsAsync()
    {
        // Get item with soft-delete control
        var product = await _repository.GetItemAsync(
            "product123", 
            "electronics", 
            includeDeleted: false);

        // Intelligent cached count with custom expiry
        var count = await _repository.GetCountWithCacheAsync(
            "electronics", 
            cacheExpiryMinutes: 30);

        // Query with array property filtering
        var premiumProducts = await _repository.GetAllByArrayPropertyAsync(
            "tags", 
            "category", 
            "premium",
            includeDeleted: false);

        // Bulk operations with detailed error handling
        try
        {
            await _repository.BulkUpsertAsync(
                products, 
                "electronics", 
                batchSize: 50, 
                maxConcurrency: 10);
        }
        catch (CosmoBaseException ex) when (ex.Data.Contains("BulkUpsertResult"))
        {
            var result = (BulkExecuteResult<ProductDocument>)ex.Data["BulkUpsertResult"];
            HandlePartialFailure(result);
        }

        // Custom LINQ queries
        var expensiveProducts = _repository.Queryable
            .Where(p => p.Price > 1000 && !p.Deleted)
            .ToAsyncEnumerable();
    }
}
```

---

## 🔧 Advanced Features

### **Intelligent Caching**

Built-in count caching with age-based invalidation:

```csharp
// Cache for 15 minutes, auto-invalidated on mutations
var count = await repository.GetCountWithCacheAsync("partition", 15);

// Force fresh count (bypass cache)
var freshCount = await repository.GetCountWithCacheAsync("partition", 0);

// Manual cache invalidation
repository.InvalidateCountCache("partition");
```

### **Comprehensive Validation**

Extensible validation system with detailed error reporting:

```csharp
// Custom validator example
public class ProductValidator : CosmosValidator<Product>
{
    public override void ValidateDocument(Product item, string operation, string partitionKeyProperty)
    {
        base.ValidateDocument(item, operation, partitionKeyProperty);
        
        // Custom business rules
        if (string.IsNullOrEmpty(item.Name))
            throw new ArgumentException("Product name is required");
            
        if (item.Price <= 0)
            throw new ArgumentException("Product price must be positive");
    }
}

// Register custom validator
services.AddSingleton<ICosmosValidator<Product>, ProductValidator>();
```

### **Soft Delete Support**

Consistent soft-delete handling across all operations:

```csharp
// Get active items only (default)
var activeProducts = await repository.GetAllByArrayPropertyAsync(
    "categories", "type", "electronics");

// Include soft-deleted items
var allProducts = await repository.GetAllByArrayPropertyAsync(
    "categories", "type", "electronics", includeDeleted: true);

// Soft delete vs hard delete
await repository.DeleteItemAsync("id", "partition", DeleteOptions.SoftDelete);
await repository.DeleteItemAsync("id", "partition", DeleteOptions.HardDelete);
```

### **Bulk Operations with Error Handling**

High-performance bulk operations with comprehensive error reporting:

```csharp
try
{
    await repository.BulkInsertAsync(documents, "partition");
}
catch (CosmoBaseException ex) when (ex.Data.Contains("BulkInsertResult"))
{
    var result = (BulkExecuteResult<Document>)ex.Data["BulkInsertResult"];
    
    Console.WriteLine($"Success rate: {result.SuccessRate:F1}%");
    Console.WriteLine($"Total RUs consumed: {result.TotalRequestUnits}");
    
    // Retry failed items that are retryable
    var retryableItems = result.FailedItems
        .Where(f => f.IsRetryable)
        .Select(f => f.Item);
    
    if (retryableItems.Any())
    {
        await repository.BulkInsertAsync(retryableItems, "partition");
    }
}
```

### **Flexible Configuration**

Multiple configuration approaches to suit different scenarios:

```csharp
// Method 1: Default configuration section
builder.Services.AddCosmoBase(builder.Configuration);

// Method 2: Custom section with overrides
builder.Services.AddCosmoBase(
    builder.Configuration.GetSection("MyCosmosSettings"),
    options => options.CosmosClientConfigurations[0].MaxRetryAttempts = 10);

// Method 3: Fully programmatic
builder.Services.AddCosmoBase(options =>
{
    options.CosmosClientConfigurations = new[]
    {
        new CosmosClientConfiguration
        {
            Name = "Default",
            ConnectionString = connectionString,
            NumberOfWorkers = 10,
            AllowBulkExecution = true
        }
    };
});
```

### **Observability & Metrics**

Built-in telemetry for monitoring and performance optimization:

```csharp
// Metrics automatically tracked:
// - cosmos.request_charge (RU consumption)
// - cosmos.retry_count (Retry attempts)
// - cosmos.cache_hit_count (Cache effectiveness)
// - cosmos.cache_miss_count (Cache misses)

// Access via standard .NET metrics APIs
// Compatible with OpenTelemetry, Prometheus, Azure Monitor
```

---

## 🛠 Configuration Reference

<details>
<summary><strong>CosmosClientConfiguration</strong></summary>

| Property                      | Type      | Description                                                                                          | Default |
| ----------------------------- | --------- | ---------------------------------------------------------------------------------------------------- | ------- |
| `Name`                        | `string`  | Unique name for this client configuration                                                           | Required |
| `ConnectionString`            | `string`  | Cosmos DB connection string                                                                          | Required |
| `NumberOfWorkers`             | `int`     | Degree of parallelism for bulk operations (1-100)                                                   | Required |
| `AllowBulkExecution`          | `bool?`   | Enable bulk operations for better throughput                                                         | `true` |
| `ConnectionMode`              | `string?` | Connection mode: "Direct" or "Gateway". Direct is faster, Gateway works better through firewalls    | `"Direct"` |
| `MaxRetryAttempts`            | `int?`    | Maximum retry attempts for rate-limited requests (0-20)                                             | `9` |
| `MaxRetryWaitTimeInSeconds`   | `int?`    | Maximum wait time in seconds for retries (1-300)                                                    | `30` |

</details>

<details>
<summary><strong>CosmosModelConfiguration</strong></summary>

| Property                              | Description                                                            |
| ------------------------------------- | ---------------------------------------------------------------------- |
| `ModelName`                           | Identifier used in code/registration (must match your DTO type)        |
| `DatabaseName`                        | Name of the Cosmos DB database                                         |
| `CollectionName`                      | Name of the container/collection                                       |
| `PartitionKey`                        | Partition key path (e.g. `/category`)                                  |
| `ReadCosmosClientConfigurationName`   | Name of the client to use for read operations                          |
| `WriteCosmosClientConfigurationName`  | Name of the client to use for write operations                         |

</details>

---

## 🧪 Testing

### **Unit Testing with Custom Configuration**

```csharp
[TestClass]
public class CosmosTests
{
    private ServiceProvider _serviceProvider;
    
    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();
        
        // Configure for Cosmos DB emulator
        services.AddCosmoBase(options =>
        {
            options.CosmosClientConfigurations = new[]
            {
                new CosmosClientConfiguration
                {
                    Name = "Test",
                    ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
                    NumberOfWorkers = 1,
                    AllowBulkExecution = false,
                    ConnectionMode = "Gateway"
                }
            };
        });
        
        _serviceProvider = services.BuildServiceProvider();
    }
    
    [TestMethod]
    public async Task TestProductOperations()
    {
        var repository = _serviceProvider.GetRequiredService<ICosmosRepository<Product>>();
        
        // Test with validation
        var product = new Product { Id = "test", Category = "electronics" };
        await repository.CreateItemAsync(product);
        
        // Test cached count
        var count = await repository.GetCountWithCacheAsync("electronics", 0);
        Assert.AreEqual(1, count);
    }
}
```

### **Mocking for Unit Tests**

```csharp
[TestMethod]
public async Task TestServiceWithMockedRepository()
{
    // Mock the repository
    var mockRepo = new Mock<ICosmosRepository<Product>>();
    mockRepo.Setup(r => r.GetCountWithCacheAsync("electronics", 15, default))
           .ReturnsAsync(42);
    
    var service = new ProductService(mockRepo.Object);
    var count = await service.GetProductCountAsync("electronics");
    
    Assert.AreEqual(42, count);
}
```

---

## 🚀 Performance Best Practices

### **Bulk Operations**
- Use batch sizes of 50-100 for optimal throughput
- Limit concurrency to 10-20 to avoid overwhelming Cosmos DB
- Handle partial failures gracefully with retry logic

### **Caching**
- Use 5-15 minute cache expiry for frequently changing data
- Use 30-60 minute cache expiry for stable reference data
- Monitor cache hit rates with built-in metrics

### **Querying**
- Use specific partition keys whenever possible
- Leverage soft-delete filtering for consistent behavior
- Stream large result sets with `IAsyncEnumerable<T>`

### **Resource Management**
- CosmoBase automatically disposes resources
- Use dependency injection for proper lifecycle management
- Monitor RU consumption with built-in telemetry

---

## 📊 Migration Guide



---

## 📄 License

This project is licensed under the [Apache License](LICENSE).

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome! Please open an issue or submit a pull request.

### **Development Setup**

1. Clone the repository
2. Install .NET 9.0 SDK
3. Run Cosmos DB emulator locally
4. Execute tests: `dotnet test`

### **Contribution Guidelines**

- Follow existing code style and patterns
- Add unit tests for new features
- Update documentation for public APIs
- Ensure all tests pass before submitting PRs

---

<p align="center">
  Made with ❤️ and 🚀 by Achilleas Tziazas
</p>