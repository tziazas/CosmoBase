# CosmoBase

[![Build Status](https://github.com/tziazas/CosmoBase/actions/workflows/publish.yml/badge.svg)](https://github.com/tziazas/CosmoBase/actions)
[![NuGet](https://img.shields.io/nuget/v/CosmoBase.CosmosDb.svg)](https://www.nuget.org/packages/CosmoBase.CosmosDb)
[![License](https://img.shields.io/github/license/tziazas/CosmoBase.svg)](LICENSE)

**CosmoBase** – Your solid foundation for building with Azure Cosmos DB: bulk operations, paging, flexible querying, and clean DTO/DAO mapping.

---

## 🏆 Features

- **Named read/write clients**: configure any number of Cosmos endpoints (primary, replicas, emulator, etc.)
- **Per-model routing**: route reads and writes to different endpoints via configuration
- **Bulk upsert**: high-throughput parallel writes with retry policies
- **Continuation-token paging**: efficient, server-side paging without re-scanning
- **LINQ & SQL queries**: expression-based or raw SQL, streamed as `IAsyncEnumerable<T>`
- **DTO ↔ DAO mapping**: zero-dependency default JSON or reflection mapper, or swap in Automapper/Mapster
- **Soft-delete & audit fields**: built-in `IsSoftDeleted`, `CreatedAtUtc`, `CreatedBy`, etc.

---

## 🚀 Installation

From the command line:

```bash
dotnet add package CosmoBase.CosmosDb
```

Or via NuGet Package Manager in Visual Studio:

```
Install-Package CosmoBase.CosmosDb
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
        "Name": "Secondary",
        "ConnectionString": "AccountEndpoint=https://myaccount-secondary.documents.azure.com:443/;AccountKey=mykey2==;",
        "NumberOfWorkers": 5,
        "AllowBulkExecution": false,
        "ConnectionMode": "Gateway",
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
        "ReadCosmosClientConfigurationName": "Primary",
        "WriteCosmosClientConfigurationName": "Primary"
      },
      {
        "ModelName": "Order",
        "DatabaseName": "OrderManagement",
        "CollectionName": "Orders",
        "PartitionKey": "/customerId",
        "ReadCosmosClientConfigurationName": "Primary",
        "WriteCosmosClientConfigurationName": "Primary"
      },
      {
        "ModelName": "Customer",
        "DatabaseName": "CustomerData",
        "CollectionName": "Customers",
        "PartitionKey": "/region",
        "ReadCosmosClientConfigurationName": "Secondary",
        "WriteCosmosClientConfigurationName": "Primary"
      },
      {
        "ModelName": "AuditLog",
        "DatabaseName": "AuditData",
        "CollectionName": "AuditLogs",
        "PartitionKey": "/date",
        "ReadCosmosClientConfigurationName": "Secondary",
        "WriteCosmosClientConfigurationName": "Secondary"
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

// Registers Cosmos clients, models, mapper, retry, repos, and data services.
builder.Services.AddCosmoBase(
    builder.Configuration.GetSection("Cosmos"),
    config =>
    {
        // optional overrides
        config.CosmosClientConfigurations
              .First(c => c.Name == "Primary")
              .NumberOfWorkers = 12;
    });

var app = builder.Build();
```

### 3. Inject the Data Services

**Preferred**—use read/write services in your application code:

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

    public async Task ProcessAsync()
    {
        // Bulk save DTOs
        var newProducts = GetNewProductDtos();
        await _writer.SaveAsync(newProducts);

        // Stream products with paging
        await foreach (var p in _reader.GetAllAsyncEnumerable(
            cancellationToken: CancellationToken.None,
            limit: 100, offset: 0, count: 500))
        {
            Handle(p);
        }
    }
}
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

## 🔧 Advanced Usage

### Configuration Options

CosmoBase provides multiple ways to configure your services:

#### Method 1: Using Default Configuration Section

The simplest approach - reads from the "CosmoBase" section in your configuration:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add CosmoBase using default "CosmoBase" configuration section
builder.Services.AddCosmoBase(builder.Configuration);

var app = builder.Build();
```

#### Method 2: Using Default Configuration Section with Options Override

Read from configuration but override specific settings:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add CosmoBase with configuration overrides
builder.Services.AddCosmoBase(builder.Configuration, options =>
{
    // Override specific settings after configuration binding
    options.CosmosClientConfigurations[0].MaxRetryAttempts = 10;
    options.CosmosClientConfigurations[0].AllowBulkExecution = true;
});

var app = builder.Build();
```

#### Method 3: Using Custom Configuration Section

Use a different configuration section name:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add CosmoBase from a custom configuration section
builder.Services.AddCosmoBase(
    builder.Configuration.GetSection("MyCosmosSettings")
);

var app = builder.Build();
```

#### Method 4: Using Custom Configuration Section with Options Override

Custom section with overrides:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add CosmoBase from custom section with overrides
builder.Services.AddCosmoBase(
    builder.Configuration.GetSection("MyCosmosSettings"),
    options =>
    {
        // Add an additional model configuration
        var newModel = new CosmosModelConfiguration
        {
            ModelName = "Analytics",
            DatabaseName = "AnalyticsDb",
            CollectionName = "Events",
            PartitionKey = "/eventType",
            ReadCosmosClientConfigurationName = "Analytics",
            WriteCosmosClientConfigurationName = "Analytics"
        };
        options.CosmosModelConfigurations.Add(newModel);
    }
);

var app = builder.Build();
```

#### Method 5: Fully Programmatic Configuration

Configure everything in code without using appsettings:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add CosmoBase with fully programmatic configuration
builder.Services.AddCosmoBase(options =>
{
    // Configure Cosmos clients
    options.CosmosClientConfigurations = new List<CosmosClientConfiguration>
    {
        new()
        {
            Name = "Primary",
            ConnectionString = builder.Configuration["CosmosDb:PrimaryConnection"],
            NumberOfWorkers = 10,
            AllowBulkExecution = true,
            ConnectionMode = "Direct",
            MaxRetryAttempts = 5,
            MaxRetryWaitTimeInSeconds = 30
        }
    };
    
    // Configure model mappings
    options.CosmosModelConfigurations = new List<CosmosModelConfiguration>
    {
        new()
        {
            ModelName = "Product",
            DatabaseName = "ProductCatalog",
            CollectionName = "Products",
            PartitionKey = "/category",
            ReadCosmosClientConfigurationName = "Primary",
            WriteCosmosClientConfigurationName = "Primary"
        },
        new()
        {
            ModelName = "Order",
            DatabaseName = "OrderManagement",
            CollectionName = "Orders",
            PartitionKey = "/customerId",
            ReadCosmosClientConfigurationName = "Primary",
            WriteCosmosClientConfigurationName = "Primary"
        }
    };
});

var app = builder.Build();
```

### Console Application Example

For non-web applications:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Configure from appsettings.json
builder.Services.AddCosmoBase(builder.Configuration);

// Or configure programmatically
builder.Services.AddCosmoBase(options =>
{
    options.CosmosClientConfigurations = new List<CosmosClientConfiguration>
    {
        new()
        {
            Name = "Default",
            ConnectionString = args[0], // From command line
            NumberOfWorkers = 5,
            AllowBulkExecution = true
        }
    };
    
    options.CosmosModelConfigurations = new List<CosmosModelConfiguration>
    {
        new()
        {
            ModelName = "MyDocument",
            DatabaseName = "MyDatabase",
            CollectionName = "MyContainer",
            PartitionKey = "/id",
            ReadCosmosClientConfigurationName = "Default",
            WriteCosmosClientConfigurationName = "Default"
        }
    };
});

var host = builder.Build();
await host.RunAsync();
```

### Testing Example

For unit tests with custom configuration:

```csharp
[TestClass]
public class CosmosTests
{
    private ServiceProvider _serviceProvider;
    
    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();
        
        // Configure for testing
        services.AddCosmoBase(options =>
        {
            options.CosmosClientConfigurations = new List<CosmosClientConfiguration>
            {
                new()
                {
                    Name = "Test",
                    ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=testkey",
                    NumberOfWorkers = 1,
                    AllowBulkExecution = false,
                    ConnectionMode = "Gateway" // Use Gateway for emulator
                }
            };
            
            options.CosmosModelConfigurations = new List<CosmosModelConfiguration>
            {
                new()
                {
                    ModelName = "TestDocument",
                    DatabaseName = "TestDb",
                    CollectionName = "TestContainer",
                    PartitionKey = "/pk",
                    ReadCosmosClientConfigurationName = "Test",
                    WriteCosmosClientConfigurationName = "Test"
                }
            };
        });
        
        _serviceProvider = services.BuildServiceProvider();
    }
}
```

### Direct Repository Access

For advanced scenarios you can still inject the low-level repository:

```csharp
public class AdvancedService
{
    private readonly ICosmosRepository<Product> _repo;

    public AdvancedService(ICosmosRepository<Product> repo)
    {
        _repo = repo;
    }

    // Use Queryable, custom LINQ expressions, or specialized methods
}
```

### Custom Mapping

Replace the default mapper:

```csharp
services.AddSingleton(typeof(IItemMapper<,>), typeof(MyCustomMapper<,>));
```

### Custom Retry Policy

Override the built-in Polly policy:

```csharp
services.RemoveAll<IAsyncPolicy>();
services.AddSingleton<IAsyncPolicy>(Policy
    .Handle<CosmosException>()
    .RetryAsync(5));
```

### Exception Handling

All CosmoBase errors derive from `CosmoBaseException`. Catch it to handle any repository or service error:

```csharp
try
{
    await _writer.SaveAsync(item);
}
catch (CosmoBaseException ex)
{
    _logger.LogError(ex, "Cosmos operation failed");
}
```

---

## 📄 License

This project is licensed under the [Apache License](LICENSE).

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome! Please open an issue or submit a pull request.

---

<p align="center">
  Made with ❤️ and 🚀 by Achilleas Tziazas
</p>