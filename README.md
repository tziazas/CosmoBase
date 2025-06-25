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
  "Cosmos": {
    "CosmosClientConfigurations": [
      {
        "Name":             "Primary",
        "ConnectionString": "<YOUR_PRIMARY_CONNECTION_STRING>",
        "NumberOfWorkers":  8
      },
      {
        "Name":             "ReadReplica",
        "ConnectionString": "<YOUR_READ_REPLICA_CONNECTION_STRING>",
        "NumberOfWorkers":  4
      }
    ],
    "CosmosModelConfigurations": [
      {
        "ModelName":                      "Product",
        "DatabaseName":                   "CatalogDb",
        "CollectionName":                 "Products",
        "PartitionKey":                   "/category",
        "ReadCosmosClientConfigurationName":  "ReadReplica",
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

| Property           | Description                                  |
| ------------------ | -------------------------------------------- |
| `Name`             | Unique name for this client configuration    |
| `ConnectionString` | Cosmos DB connection string                  |
| `NumberOfWorkers`  | Degree of parallelism for bulk operations     |

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

This project is licensed under the [MIT License](LICENSE).

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome! Please open an issue or submit a pull request.

---

<p align="center">
  Made with ❤️ and 🚀 by tziazas
</p>
