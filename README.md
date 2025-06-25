# CosmoBase

![CosmoBase logo](docs/images/cosmobase-logo.png)

**CosmoBase** – Your solid foundation for building with Azure Cosmos DB.

## Install

```bash
dotnet add package CosmoBase.CosmosDb
```

## Structure
```
CosmoBase/                    ← repo root (matches your GitHub name)
│
├── .github/                  ← GitHub Automation & CI/CD
│   └── workflows/
│       └── publish.yml       ← build & pack & push to NuGet on tags
│
├── docs/                     ← (optional) GitHub Pages or DocFX site
│   ├── images/               
│   │   └── cosmobase-logo.png
│   └── index.md              ← landing page for your docs site
│
├── src/                      ← all your library code
│   ├── CosmoBase.Abstractions/  
│   │   ├── Configuration/    ← options POCOs
│   │   ├── Enums/            ← ComparisonOperator, etc.
│   │   ├── Filters/          ← PropertyFilter, IFilterDefinition…
│   │   ├── Interfaces/       ← IItemMapper<>, ICosmosRepository<>, IDataReadService<>, …
│   │   └── CosmoBase.Abstractions.csproj
│   │
│   └── CosmoBase/            ← implementation project
│       ├── Configuration/    ← MyCosmosOptions<TDao,TDto>, named-options setup
│       ├── DependencyInjection/ ← your AddCosmoBase… extension methods
│       ├── Repositories/     ← CosmosRepository<TDao,TDto>, etc.
│       ├── DataServices/     ← CosmosDataReadService<TDto>, CosmosDataWriteService<TDto>
│       ├── Services/         ← CosmosHelpersClient<TDao,TDto>
│       ├── Filters/          ← any filter-to-SQL translators or expression visitors
│       └── CosmoBase.csproj
│
├── tests/                    ← unit & integration tests
│   ├── CosmoBase.Abstractions.Tests/
│   └── CosmoBase.Tests/
│
├── build/                    ← (optional) local scripts, e.g. version bump, packaging
│   └── bump-version.ps1      
│
├── CosmoBase.sln             ← solution file tying src/ & tests/ together
├── README.md                 ← repo intro, install instructions, logo embed
├── LICENSE                   ← MIT or Apache-2.0
└── .gitignore
```

## Sample configuration
```
{
  "Cosmos": {
    "CosmosClientConfigurations": [
      {
        "Name":              "Primary",
        "ConnectionString":  "<master-endpoint-connstr>",
        "NumberOfWorkers":   8
      },
      {
        "Name":              "ReadReplica",
        "ConnectionString":  "<read-replica-connstr>",
        "NumberOfWorkers":   4
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
      },
      {
        "ModelName":                      "Order",
        "DatabaseName":                   "SalesDb",
        "CollectionName":                 "Orders",
        "PartitionKey":                   "/customerId",
        "ReadCosmosClientConfigurationName":  "Primary",
        "WriteCosmosClientConfigurationName": "Primary"
      }
    ]
  }
}
```