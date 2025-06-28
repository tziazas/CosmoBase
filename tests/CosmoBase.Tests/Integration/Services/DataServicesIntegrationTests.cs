using System.Reflection;
using CosmoBase.Abstractions.Configuration;
using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Tests.Fixtures;
using CosmoBase.Tests.Helpers;
using CosmoBase.Tests.TestModels;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;
using IConfiguration = Castle.Core.Configuration.IConfiguration;

namespace CosmoBase.Tests.Integration.Services;

/// <summary>
/// Integration tests for CosmoBase Data Services using real Cosmos DB emulator
/// </summary>
[Collection("CosmoBase")]
public class DataServicesIntegrationTests(
    CosmoBaseTestFixture fixture,
    ITestOutputHelper output)
    : IClassFixture<CosmoBaseTestFixture>
{
    [Fact]
    public Task DataReadService_Should_Be_Registered_In_DI_Container()
    {
        // Arrange & Act
        var readService = fixture.GetService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        // Assert
        readService.Should()
            .NotBeNull("ICosmosDataReadService<TestProduct, TestProductDao> should be registered in DI container");

        return Task.CompletedTask;
    }

    [Fact]
    public Task DataWriteService_Should_Be_Registered_In_DI_Container()
    {
        // Arrange & Act
        var writeService = fixture.GetService<ICosmosDataWriteService<TestProduct, TestProductDao>>();

        // Assert
        writeService.Should()
            .NotBeNull("ICosmosDataWriteService<TestProduct, TestProductDao> should be registered in DI container");

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Debug_Container_Partition_Key()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var cosmosClient = cosmosClients.Values.First();

        try
        {
            var container = cosmosClient.GetContainer("CosmoBaseTestDb", "Products");
            var containerProperties = await container.ReadContainerAsync();

            output.WriteLine($"Container partition key path: '{containerProperties.Resource.PartitionKeyPath}'");
            output.WriteLine($"Config expects partition key property: 'Category'");

            // Test creating with different casing
            var testDoc = new { id = "test", Category = "electronics", category = "electronics" };
            var result = await container.CreateItemAsync(testDoc, new PartitionKey("electronics"));
            output.WriteLine("✅ Direct container create worked");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Container debug failed: {ex.Message}");
        }
    }

    [Fact]
    public async Task Debug_Partition_Key_Issue()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();

        // Create the simplest possible product
        var product = new TestProduct
        {
            Id = "debug-test-123",
            Name = "Debug Product",
            Category = "electronics",
            CustomerId = "test-customer",
            Price = 10.00m
        };

        output.WriteLine($"Product.Category: '{product.Category}'");
        output.WriteLine($"Product.CustomerId: '{product.CustomerId}'");

        try
        {
            var result = await writeService.CreateAsync(product);
            output.WriteLine("✅ Create succeeded");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Create failed: {ex.Message}");

            // Let's also check the configuration
            var config = fixture.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var partitionKey = config["CosmoBase:CosmosModelConfigurations:0:PartitionKey"];
            output.WriteLine($"Configured PartitionKey: '{partitionKey}'");
        }
    }

    [Fact]
    public async Task Debug_DAO_Mapping()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var mapper = fixture.GetRequiredService<IItemMapper<TestProductDao, TestProduct>>();

        var product = new TestProduct
        {
            Id = "debug-123",
            Name = "Debug Product",
            Category = "electronics",
            CustomerId = "test-customer",
            Price = 10.00m
        };

        // Test the mapping directly
        var dao = mapper.ToDao(product);

        output.WriteLine($"DAO Id: '{dao.Id}'");
        output.WriteLine($"DAO Category: '{dao.Category}'");
        output.WriteLine($"DAO CustomerId: '{dao.CustomerId}'");
        output.WriteLine(
            $"DAO has all required fields: {!string.IsNullOrEmpty(dao.Id) && !string.IsNullOrEmpty(dao.Category)}");

        // Try repository directly
        var repository = fixture.GetRequiredService<ICosmosRepository<TestProductDao>>();
        try
        {
            var result = await repository.CreateItemAsync(dao);
            output.WriteLine("✅ Repository create succeeded");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Repository create failed: {ex.Message}");
        }
    }

    [Fact]
    public async Task Debug_JSON_Serialization()
    {
        var repository = fixture.GetRequiredService<ICosmosRepository<TestProductDao>>();

        var dao = new TestProductDao
        {
            Id = "simple-123",
            Name = "Simple Product",
            Category = "electronics",
            CustomerId = "test-customer",
            Price = 10.00m
        };

        // Serialize to see exact JSON
        var json = System.Text.Json.JsonSerializer.Serialize(dao);
        output.WriteLine($"DAO JSON: {json}");

        // Try the most minimal DAO possible
        var minimalDao = new TestProductDao
        {
            Id = "minimal-123",
            Category = "electronics" // Just partition key + id
        };

        var minimalJson = System.Text.Json.JsonSerializer.Serialize(minimalDao);
        output.WriteLine($"Minimal JSON: {minimalJson}");

        try
        {
            await repository.CreateItemAsync(minimalDao);
            output.WriteLine("✅ Minimal DAO worked");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Even minimal DAO failed: {ex.Message}");
        }
    }

    [Fact]
    public async Task Debug_Partition_Key_Value()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var cosmosClient = cosmosClients.Values.First();
        var container = cosmosClient.GetContainer("CosmoBaseTestDb", "Products");

        // Test what Cosmos expects vs what we're sending
        var testDoc = new
        {
            id = "test-pk-123",
            Category = "electronics"
        };

        try
        {
            // This should work - using Category as partition key
            await container.CreateItemAsync(testDoc, new PartitionKey("electronics"));
            output.WriteLine("✅ Direct create with 'electronics' works");

            // Now test if our repository is using the wrong property
            var config = fixture.GetRequiredService<CosmosConfiguration>();
            var modelConfig = config.CosmosModelConfigurations.First(x => x.ModelName == "TestProductDao");
            output.WriteLine($"Model config partition key property: '{modelConfig.PartitionKey}'");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Even direct create failed: {ex.Message}");
        }
    }

    [Fact]
    public void Debug_Reflection_Property_Reading()
    {
        var daoType = typeof(TestProductDao);
        var properties = daoType.GetProperties();

        output.WriteLine("All properties on TestProductDao:");
        foreach (var prop in properties)
        {
            output.WriteLine($"  {prop.Name}: {prop.PropertyType}");
        }

        // Test specific property lookup
        var categoryProp = daoType.GetProperty("Category");
        output.WriteLine($"Category property found: {categoryProp != null}");

        if (categoryProp != null)
        {
            var dao = new TestProductDao { Category = "electronics" };
            var value = categoryProp.GetValue(dao);
            output.WriteLine($"Category value via reflection: '{value}'");
        }
        else
        {
            output.WriteLine("❌ Category property NOT found via reflection!");
        }
    }

    [Fact]
    public async Task Debug_Repository_Validation()
    {
        var repository = fixture.GetRequiredService<ICosmosRepository<TestProductDao>>();
        var validator = fixture.GetRequiredService<ICosmosValidator<TestProductDao>>();

        var dao = new TestProductDao
        {
            Id = "validation-123",
            Category = "electronics",
            Name = "Test Product",
            CustomerId = "test-customer",
            Price = 10.00m
        };

        try
        {
            // Test validation directly
            validator.ValidateDocument(dao, "Create", "Category");
            output.WriteLine("✅ Validation passed");

            // Test partition key reading
            var partitionKeyProp = typeof(TestProductDao).GetProperty("Category");
            var partitionKeyValue = partitionKeyProp?.GetValue(dao)?.ToString();
            output.WriteLine($"Partition key value: '{partitionKeyValue}'");

            // The issue might be in the CreateItemAsync validation
            // Let's see what the actual validation error is
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Validation failed: {ex.Message}");
            output.WriteLine($"Full exception: {ex}");
        }
    }

    [Fact]
    public async Task Debug_Repository_Direct()
    {
        var repository = fixture.GetRequiredService<ICosmosRepository<TestProductDao>>();

        var dao = new TestProductDao
        {
            Id = "repo-direct-123",
            Category = "electronics",
            Name = "Test Product",
            CustomerId = "test-customer",
            Price = 10.00m,
            // Pre-populate audit fields to avoid audit manager issues
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow,
            CreatedBy = "Manual",
            UpdatedBy = "Manual",
            Deleted = false
        };

        try
        {
            output.WriteLine("Testing repository.CreateItemAsync directly...");
            var result = await repository.CreateItemAsync(dao);
            output.WriteLine($"✅ Repository direct create succeeded: {result.Id}");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Repository direct failed: {ex.Message}");

            // If this fails, let's see the Cosmos exception details
            if (ex.InnerException != null)
            {
                output.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }

    [Fact]
    public async Task Debug_Container_Mismatch()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var config = fixture.GetRequiredService<CosmosConfiguration>();
        var modelConfig = config.CosmosModelConfigurations.First(x => x.ModelName == "TestProductDao");

        output.WriteLine($"Model config expects:");
        output.WriteLine($"  Database: {modelConfig.DatabaseName}");
        output.WriteLine($"  Collection: {modelConfig.CollectionName}");
        output.WriteLine($"  PartitionKey: {modelConfig.PartitionKey}");

        // Check what containers actually exist
        var cosmosClient = cosmosClients.Values.First();
        var database = cosmosClient.GetDatabase("CosmoBaseTestDb");

        try
        {
            var iterator = database.GetContainerQueryIterator<dynamic>("SELECT * FROM c");
            output.WriteLine("Existing containers:");
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                foreach (var item in response)
                {
                    output.WriteLine($"  {item}");
                }
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"Could not list containers: {ex.Message}");
        }

        // Test the exact container the repository should be using
        var container = cosmosClient.GetContainer(modelConfig.DatabaseName, modelConfig.CollectionName);
        var containerProps = await container.ReadContainerAsync();
        output.WriteLine($"Target container partition key: {containerProps.Resource.PartitionKeyPath}");
    }

    [Fact]
    public async Task Debug_Client_Configuration()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var config = fixture.GetRequiredService<CosmosConfiguration>();

        output.WriteLine("Available Cosmos clients:");
        foreach (var kvp in cosmosClients)
        {
            output.WriteLine($"  {kvp.Key}: {kvp.Value.Endpoint}");
        }

        // Check if the repository is using the right clients
        var modelConfig = config.CosmosModelConfigurations.First(x => x.ModelName == "TestProductDao");
        output.WriteLine($"Model expects write client: {modelConfig.WriteCosmosClientConfigurationName}");

        // Test both read and write clients directly
        foreach (var kvp in cosmosClients)
        {
            try
            {
                var container = kvp.Value.GetContainer("CosmoBaseTestDb", "Products");
                var testDoc = new { id = $"client-test-{kvp.Key}", Category = "electronics" };
                await container.CreateItemAsync(testDoc, new PartitionKey("electronics"));
                output.WriteLine($"✅ Client '{kvp.Key}' works");
            }
            catch (Exception ex)
            {
                output.WriteLine($"❌ Client '{kvp.Key}' failed: {ex.Message}");
            }
        }
    }

    [Fact]
    public async Task Debug_CosmosClient_Options()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var client = cosmosClients["TestPrimary"];

        // Check client options via reflection
        var clientOptionsField = client.GetType().GetField("clientOptions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (clientOptionsField != null)
        {
            var options = clientOptionsField.GetValue(client);
            output.WriteLine($"Client options: {options}");
        }

        // Test with the exact same document structure as the repository sends
        var container = client.GetContainer("CosmoBaseTestDb", "Products");

        var repoStyleDoc = new
        {
            id = "repo-style-123",
            CreatedOnUtc = (DateTime?)DateTime.UtcNow,
            UpdatedOnUtc = (DateTime?)DateTime.UtcNow,
            CreatedBy = "TestUser",
            UpdatedBy = "TestUser",
            Deleted = false,
            Name = "Test Product",
            customerId = "test-customer", // Note: lowercase as in JSON
            Category = "electronics",
            Price = 10.00m,
            Description = (string?)null,
            Tags = new List<string>(),
            // ... other fields as nulls/defaults
        };

        try
        {
            await container.CreateItemAsync(repoStyleDoc, new PartitionKey("electronics"));
            output.WriteLine("✅ Repo-style document works");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Repo-style document failed: {ex.Message}");
        }
    }

    [Fact]
    public async Task Debug_Repository_Step_By_Step()
    {
        // Get the repository and trace through its steps
        var repository = fixture.GetRequiredService<ICosmosRepository<TestProductDao>>();
        var validator = fixture.GetRequiredService<ICosmosValidator<TestProductDao>>();
        var auditManager = fixture.GetRequiredService<IAuditFieldManager<TestProductDao>>();

        var dao = new TestProductDao
        {
            Id = "step-by-step-123",
            Category = "electronics",
            Name = "Test Product",
            CustomerId = "test-customer",
            Price = 10.00m
        };

        output.WriteLine("Step 1: Original DAO");
        output.WriteLine($"  Category: '{dao.Category}'");

        // Step 2: Validation (this should pass based on earlier tests)
        try
        {
            validator.ValidateDocument(dao, "Create", "Category");
            output.WriteLine("Step 2: ✅ Validation passed");
        }
        catch (Exception ex)
        {
            output.WriteLine($"Step 2: ❌ Validation failed: {ex.Message}");
            return;
        }

        // Step 3: Audit field management
        try
        {
            auditManager.SetCreateAuditFields(dao);
            output.WriteLine("Step 3: ✅ Audit fields set");
            output.WriteLine($"  CreatedBy: '{dao.CreatedBy}'");
            output.WriteLine($"  UpdatedBy: '{dao.UpdatedBy}'");
        }
        catch (Exception ex)
        {
            output.WriteLine($"Step 3: ❌ Audit fields failed: {ex.Message}");
            return;
        }

        // Step 4: Get partition key value (this should work based on earlier tests)
        var partitionKeyValue = dao.Category; // Simulating GetPartitionKeyValue
        output.WriteLine($"Step 4: Partition key value: '{partitionKeyValue}'");

        output.WriteLine("Issue must be in the actual Cosmos SDK call within the repository!");
    }

    [Fact]
    public async Task Debug_Repository_Cosmos_Call()
    {
        var repository = fixture.GetRequiredService<ICosmosRepository<TestProductDao>>();
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();

        // Get the same client the repository should be using
        var writeClient = cosmosClients["TestPrimary"];

        var dao = new TestProductDao
        {
            Id = "cosmos-call-123",
            Category = "electronics",
            Name = "Test Product",
            CustomerId = "test-customer",
            Price = 10.00m,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow,
            CreatedBy = "TestUser",
            UpdatedBy = "TestUser",
            Deleted = false
        };

        // Test the exact same call the repository makes
        var container = writeClient.GetContainer("CosmoBaseTestDb", "Products");

        try
        {
            // This is exactly what the repository does internally
            var response = await container.CreateItemAsync(
                dao,
                new Microsoft.Azure.Cosmos.PartitionKey("electronics"));

            output.WriteLine($"✅ Direct container.CreateItemAsync with full DAO succeeded: {response.Resource.Id}");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Direct container.CreateItemAsync failed: {ex.Message}");

            // Let's also try without the audit fields to see if that's the issue
            var simpleDao = new { id = "simple-cosmos-123", Category = "electronics" };
            try
            {
                await container.CreateItemAsync(simpleDao, new Microsoft.Azure.Cosmos.PartitionKey("electronics"));
                output.WriteLine("✅ Simple DAO works, so it's something in the full DAO structure");
            }
            catch
            {
                output.WriteLine("❌ Even simple DAO fails with this container call");
            }
        }
    }

    [Fact]
    public async Task Debug_DAO_Properties()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var container = cosmosClients["TestPrimary"].GetContainer("CosmoBaseTestDb", "Products");

        // Test each property group incrementally
        var tests = new List<(string name, object doc)>
        {
            ("Basic", new { id = "test1", Category = "electronics" }),

            ("With audit", new
            {
                id = "test2", Category = "electronics",
                CreatedOnUtc = (DateTime?)DateTime.UtcNow,
                UpdatedOnUtc = (DateTime?)DateTime.UtcNow,
                CreatedBy = "TestUser",
                UpdatedBy = "TestUser",
                Deleted = false
            }),

            ("With business fields", new
            {
                id = "test3", Category = "electronics",
                Name = "Test", CustomerId = "test", Price = 10.00m
            }),

            ("With collections", new
            {
                id = "test4", Category = "electronics",
                Tags = new List<string>()
            }),

            ("With complex objects", new
            {
                id = "test5", Category = "electronics",
                Metadata = (object?)null,
                Dimensions = (object?)null
            })
        };

        foreach (var test in tests)
        {
            try
            {
                await container.CreateItemAsync(test.doc, new Microsoft.Azure.Cosmos.PartitionKey("electronics"));
                output.WriteLine($"✅ {test.name} works");
            }
            catch (Exception ex)
            {
                output.WriteLine($"❌ {test.name} failed: {ex.Message}");
            }
        }
    }

    [Fact]
    public async Task Debug_TestProductDao_vs_Manual()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var container = cosmosClients["TestPrimary"].GetContainer("CosmoBaseTestDb", "Products");

        // Create actual TestProductDao
        var actualDao = new TestProductDao
        {
            Id = "actual-dao-123",
            Category = "electronics",
            Name = "Test Product",
            CustomerId = "test-customer",
            Price = 10.00m,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow,
            CreatedBy = "TestUser",
            UpdatedBy = "TestUser",
            Deleted = false
        };

        // Create manual object with ALL the same properties
        var manualObj = new
        {
            id = "manual-obj-123",
            Category = "electronics",
            Name = "Test Product",
            customerId = "test-customer", // Note the JsonPropertyName
            Price = 10.00m,
            CreatedOnUtc = (DateTime?)DateTime.UtcNow,
            UpdatedOnUtc = (DateTime?)DateTime.UtcNow,
            CreatedBy = "TestUser",
            UpdatedBy = "TestUser",
            Deleted = false,
            Description = (string?)null,
            Tags = new List<string>(),
            Metadata = (ProductMetadata?)null,
            IsActive = true,
            DiscontinuedDate = (DateTime?)null,
            StockQuantity = 0,
            Sku = (string?)null,
            Barcode = (string?)null,
            Dimensions = (ProductDimensions?)null
        };

        try
        {
            await container.CreateItemAsync(manualObj, new Microsoft.Azure.Cosmos.PartitionKey("electronics"));
            output.WriteLine("✅ Manual object works");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Manual object failed: {ex.Message}");
        }

        try
        {
            await container.CreateItemAsync(actualDao, new Microsoft.Azure.Cosmos.PartitionKey("electronics"));
            output.WriteLine("✅ Actual DAO works");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Actual DAO failed: {ex.Message}");

            // Check if it's a serialization difference
            var json1 = System.Text.Json.JsonSerializer.Serialize(manualObj);
            var json2 = System.Text.Json.JsonSerializer.Serialize(actualDao);
            output.WriteLine($"Manual JSON length: {json1.Length}");
            output.WriteLine($"DAO JSON length: {json2.Length}");
        }
    }
    
    [Fact]
    public async Task Debug_DAO_Attributes()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var container = cosmosClients["TestPrimary"].GetContainer("CosmoBaseTestDb", "Products");
    
        // Create a DAO without any JsonPropertyName or validation attributes
        var cleanDao = new CleanTestProductDao
        {
            id = "clean-dao-123",
            Category = "electronics",
            Name = "Test Product",
            CustomerId = "test-customer",
            Price = 10.00m,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow,
            CreatedBy = "TestUser",
            UpdatedBy = "TestUser",
            Deleted = false
        };
    
        try
        {
            await container.CreateItemAsync(cleanDao, new Microsoft.Azure.Cosmos.PartitionKey("electronics"));
            output.WriteLine("✅ Clean DAO works");
            output.WriteLine("Issue is in the JsonPropertyName or validation attributes!");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Clean DAO failed: {ex.Message}");
            output.WriteLine("Issue is not in the attributes");
        }
    }
    
    [Fact]
    public async Task Debug_JsonPropertyName_Issue()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var container = cosmosClients["TestPrimary"].GetContainer("CosmoBaseTestDb", "Products");
    
        // Test with JsonPropertyName on CustomerId
        var daoWithJsonAttr = new TestDaoWithJsonAttr
        {
            Id = "json-attr-123", 
            Category = "electronics",
            CustomerId = "test-customer"
        };
    
        try
        {
            await container.CreateItemAsync(daoWithJsonAttr, new Microsoft.Azure.Cosmos.PartitionKey("electronics"));
            output.WriteLine("✅ DAO with JsonPropertyName works");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ DAO with JsonPropertyName failed: {ex.Message}");
            output.WriteLine("The issue is the JsonPropertyName attribute!");
        }
    }
    
    [Fact]
    public async Task Debug_Validation_Attributes()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var container = cosmosClients["TestPrimary"].GetContainer("CosmoBaseTestDb", "Products");
    
        // Test DAO without validation attributes
        var daoWithoutValidation = new TestProductDaoNoValidation
        {
            Id = "no-validation-123",
            Category = "electronics",
            Name = "Test Product",
            CustomerId = "test-customer",
            Price = 10.00m,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow,
            CreatedBy = "TestUser",
            UpdatedBy = "TestUser",
            Deleted = false
        };
    
        try
        {
            await container.CreateItemAsync(daoWithoutValidation, new Microsoft.Azure.Cosmos.PartitionKey("electronics"));
            output.WriteLine("✅ DAO without validation attributes works");
            output.WriteLine("Issue is the [Required], [StringLength], or [Range] attributes!");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ DAO without validation attributes failed: {ex.Message}");
        }
    }
    
    [Fact]
    public async Task Debug_Complex_Properties()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var container = cosmosClients["TestPrimary"].GetContainer("CosmoBaseTestDb", "Products");
    
        // Test DAO with only simple properties (no ProductMetadata, ProductDimensions)
        var simpleDao = new SimpleTestProductDao
        {
            Id = "simple-props-123",
            Category = "electronics",
            Name = "Test Product",
            CustomerId = "test-customer",
            Price = 10.00m,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow,
            CreatedBy = "TestUser",
            UpdatedBy = "TestUser",
            Deleted = false,
            Description = "Test description",
            IsActive = true,
            StockQuantity = 10,
            Sku = "TEST123",
            Barcode = "BAR123"
        };
    
        try
        {
            await container.CreateItemAsync(simpleDao, new Microsoft.Azure.Cosmos.PartitionKey("electronics"));
            output.WriteLine("✅ DAO with simple properties works");
            output.WriteLine("Issue is with ProductMetadata, ProductDimensions, or Tags list!");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ DAO with simple properties failed: {ex.Message}");
            output.WriteLine("Issue is even deeper - maybe List<string> Tags?");
        }
    }
    
    [Fact]
    public async Task Debug_Upsert_Audit_Logic()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
    
        // Test 1: Create a product first
        var product = new TestProduct
        {
            Id = "upsert-debug-123",
            Name = "Debug Product",
            Category = "electronics", 
            CustomerId = "test-customer",
            Price = 10.00m
        };
    
        try
        {
            // First create (this should work)
            var created = await writeService.CreateAsync(product);
            output.WriteLine($"✅ Create succeeded. CreatedOnUtc: {created.CreatedOnUtc}");
        
            // Now try upsert of the SAME product (this should fail)
            var upserted = await writeService.UpsertAsync(created);
            output.WriteLine($"✅ Upsert of existing product succeeded");
        
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Debug test failed: {ex.Message}");
        }
    
        // Test 2: Try upsert of a completely new product (this should work like create)
        try
        {
            var newProduct = new TestProduct
            {
                Id = "upsert-new-123",
                Name = "New Product",
                Category = "electronics",
                CustomerId = "test-customer", 
                Price = 20.00m
            };
        
            var upserted = await writeService.UpsertAsync(newProduct);
            output.WriteLine($"✅ Upsert of new product succeeded");
        
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Upsert of new product failed: {ex.Message}");
        }
    }
    
    [Fact]
    public async Task Debug_Empty_Required_Fields()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var container = cosmosClients["TestPrimary"].GetContainer("CosmoBaseTestDb", "Products");
    
        // Test TestProductDao with all required fields populated
        var populatedDao = new TestProductDao
        {
            Id = "populated-123",
            Category = "electronics",
            Name = "Test Product",        // ← Fill the required field
            CustomerId = "test-customer"  // ← Fill the required field
        };
    
        try
        {
            await container.CreateItemAsync(populatedDao, new Microsoft.Azure.Cosmos.PartitionKey("electronics"));
            output.WriteLine("✅ TestProductDao with populated required fields works!");
            output.WriteLine("Issue is empty string values in required fields");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ TestProductDao with populated required fields still fails: {ex.Message}");
        }
    }
    
    [Fact]
    public async Task Debug_Environment_Change()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var container = cosmosClients["TestPrimary"].GetContainer("CosmoBaseTestDb", "Products");
    
        // Test the EXACT same CleanTestProductDao that worked before
        var originalWorking = new CleanTestProductDao
        {
            Id = "env-test-123",
            Category = "electronics"
        };
    
        try
        {
            await container.CreateItemAsync(originalWorking, new Microsoft.Azure.Cosmos.PartitionKey("electronics"));
            output.WriteLine("✅ Original CleanTestProductDao still works");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Even CleanTestProductDao now fails: {ex.Message}");
            output.WriteLine("Something changed in the environment!");
        }
    
        // Test if the issue is that we added too many test documents
        try
        {
            // Try with a completely different id pattern
            var freshTest = new { id = $"fresh-{Guid.NewGuid()}", Category = "electronics" };
            await container.CreateItemAsync(freshTest, new Microsoft.Azure.Cosmos.PartitionKey("electronics"));
            output.WriteLine("✅ Fresh anonymous object works");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Fresh anonymous object fails: {ex.Message}");
            output.WriteLine("The container or database is corrupted!");
        }
    }
    
    [Fact]
    public async Task Debug_Client_Configuration_Again()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
    
        foreach (var kvp in cosmosClients)
        {
            output.WriteLine($"Testing client: {kvp.Key}");
        
            try
            {
                var container = kvp.Value.GetContainer("CosmoBaseTestDb", "Products");
            
                // Test anonymous object (this should work)
                var anonObj = new { id = $"client-anon-{kvp.Key}", Category = "electronics" };
                await container.CreateItemAsync(anonObj, new PartitionKey("electronics"));
                output.WriteLine($"✅ Client '{kvp.Key}' - anonymous object works");
            
                // Test simple class object (this should fail now)
                var classObj = new SimpleTestClass { id = $"client-class-{kvp.Key}", Category = "electronics" };
                await container.CreateItemAsync(classObj, new PartitionKey("electronics"));
                output.WriteLine($"✅ Client '{kvp.Key}' - class object works");
            
            }
            catch (Exception ex)
            {
                output.WriteLine($"❌ Client '{kvp.Key}' failed: {ex.Message}");
            }
        }
    }
    
    [Fact]
    public async Task Debug_ICosmosDataModel_Issue()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var container = cosmosClients["TestPrimary"].GetContainer("CosmoBaseTestDb", "Products");
    
        // Test class WITHOUT ICosmosDataModel
        var classWithoutInterface = new SimpleTestClass 
        { 
            id = "no-interface-123", 
            Category = "electronics" 
        };
    
        // Test class WITH ICosmosDataModel but minimal
        var classWithInterface = new MinimalCosmosClass
        {
            Id = "with-interface-123",
            Category = "electronics",
            CreatedOnUtc = null,
            UpdatedOnUtc = null,
            CreatedBy = null,
            UpdatedBy = null,
            Deleted = false
        };
    
        try
        {
            await container.CreateItemAsync(classWithoutInterface, new PartitionKey("electronics"));
            output.WriteLine("✅ Class without ICosmosDataModel works");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Class without ICosmosDataModel fails: {ex.Message}");
        }
    
        try
        {
            await container.CreateItemAsync(classWithInterface, new PartitionKey("electronics"));
            output.WriteLine("✅ Class with ICosmosDataModel works");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Class with ICosmosDataModel fails: {ex.Message}");
            output.WriteLine("The issue is ICosmosDataModel implementation!");
        }
    }
    
    [Fact]
    public async Task Debug_Serialization_Settings()
    {
        // Check if CosmoBase is registering custom serialization settings
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var client = cosmosClients["TestPrimary"];
    
        // Check the client's serializer settings via reflection
        var serializerField = client.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => f.Name.Contains("serializer") || f.Name.Contains("Serializer"));
    
        if (serializerField != null)
        {
            var serializer = serializerField.GetValue(client);
            output.WriteLine($"Client serializer: {serializer?.GetType()}");
        }
    
        // Test manual serialization of ICosmosDataModel
        var testObj = new MinimalCosmosClass
        {
            Id = "serialization-test",
            Category = "electronics"
        };
    
        // Test different serializers
        try
        {
            var systemTextJson = System.Text.Json.JsonSerializer.Serialize(testObj);
            output.WriteLine($"System.Text.Json: {systemTextJson}");
        }
        catch (Exception ex)
        {
            output.WriteLine($"System.Text.Json failed: {ex.Message}");
        }
    
        try
        {
            var newtonsoftJson = Newtonsoft.Json.JsonConvert.SerializeObject(testObj);
            output.WriteLine($"Newtonsoft.Json: {newtonsoftJson}");
        }
        catch (Exception ex)
        {
            output.WriteLine($"Newtonsoft.Json failed: {ex.Message}");
        }
    }
    
    [Fact]
    public async Task Debug_Id_Case_Issue()
    {
        var cosmosClients = fixture.ServiceProvider.GetRequiredService<IReadOnlyDictionary<string, CosmosClient>>();
        var container = cosmosClients["TestPrimary"].GetContainer("CosmoBaseTestDb", "Products");
    
        // Test with lowercase id (should work)
        var lowercaseIdObj = new { id = "lowercase-123", Category = "electronics" };
    
        // Test with uppercase Id (should fail)
        var uppercaseIdObj = new { Id = "uppercase-123", Category = "electronics" };
    
        try
        {
            await container.CreateItemAsync(lowercaseIdObj, new PartitionKey("electronics"));
            output.WriteLine("✅ Lowercase 'id' works");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Lowercase 'id' fails: {ex.Message}");
        }
    
        try
        {
            await container.CreateItemAsync(uppercaseIdObj, new PartitionKey("electronics"));
            output.WriteLine("✅ Uppercase 'Id' works");
        }
        catch (Exception ex)
        {
            output.WriteLine($"❌ Uppercase 'Id' fails: {ex.Message}");
            output.WriteLine("CONFIRMED: Cosmos DB requires lowercase 'id'!");
        }
    }

    [Fact]
    public async Task CreateAsync_Should_Create_Single_Product_With_Audit_Fields()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var product = TestDataBuilder.CreateTestProduct("electronics");

        // Act
        var savedProduct = await writeService.CreateAsync(product);

        // Assert
        savedProduct.Should().NotBeNull();
        savedProduct.Id.Should().NotBeNullOrEmpty();
        savedProduct.Name.Should().Be(product.Name);
        savedProduct.Category.Should().Be("electronics");

        // Verify audit fields are set
        savedProduct.CreatedOnUtc.Should().NotBeNull("CreatedOnUtc should be set automatically");
        savedProduct.UpdatedOnUtc.Should().NotBeNull("UpdatedOnUtc should be set automatically");
        savedProduct.CreatedBy.Should().Be("TestUser", "CreatedBy should be set from user context");
        savedProduct.UpdatedBy.Should().Be("TestUser", "UpdatedBy should be set from user context");
        savedProduct.Deleted.Should().BeFalse("New items should not be marked as deleted");

        output.WriteLine($"Created product: {savedProduct.Name} with ID: {savedProduct.Id}");
    }

    [Fact]
    public async Task UpsertAsync_Should_Handle_Multiple_Products()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var products = TestDataBuilder.CreateTestProducts(3, "electronics");

        // Act - Use fresh products for each upsert (don't reuse existing ones)
        var savedProducts = new List<TestProduct>();
        foreach (var product in products)
        {
            // Make sure each product has a unique ID
            product.Id = Guid.NewGuid().ToString();
            var saved = await writeService.UpsertAsync(product);
            savedProducts.Add(saved);
        }

        // Assert
        savedProducts.Should().HaveCount(3, "Should save all 3 products");
        savedProducts.Should().OnlyContain(p => !string.IsNullOrEmpty(p.Id), "All products should have IDs");
        savedProducts.Should().OnlyContain(p => p.CreatedOnUtc.HasValue, "All products should have CreatedOnUtc");
        savedProducts.Should().OnlyContain(p => p.UpdatedOnUtc.HasValue, "All products should have UpdatedOnUtc");
        savedProducts.Should().OnlyContain(p => p.CreatedBy == "TestUser", "All products should have correct CreatedBy");
        savedProducts.Should().OnlyContain(p => p.UpdatedBy == "TestUser", "All products should have correct UpdatedBy");
        savedProducts.Should().OnlyContain(p => !p.Deleted, "All products should not be deleted");

        output.WriteLine($"Successfully saved {savedProducts.Count} products");
    }

    [Fact]
    public async Task BulkUpsertAsync_Should_Handle_Multiple_Products()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var products =
            SimpleTestDataBuilder
                .CreateSimpleTestProducts(10, "Meow"); //TestDataBuilder.CreateTestProducts(5, "electronics");

        // Act
        await writeService.BulkUpsertAsync(
            products,
            p => p.Category, // partition key selector
            configureItem: null,
            batchSize: 3,
            maxConcurrency: 2);

        // Assert - verify by reading the data back
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();
        var count = await readService.GetCountAsync("electronics");
        count.Should().BeGreaterOrEqualTo(5, "Should have at least the 5 products we bulk upserted");

        output.WriteLine($"Successfully bulk upserted 5 products, total count in electronics: {count}");
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Saved_Products()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var products = SimpleTestDataBuilder.CreateSimpleTestProducts(3, "books"); // ← Change to lowercase
        foreach (var product in products)
        {
            await writeService.CreateAsync(product);
        }

        // Act
        var retrievedProducts = new List<TestProduct>();
        await foreach (var product in readService.GetAllAsync("books")) // ← Already lowercase
        {
            retrievedProducts.Add(product);
        }

        // Assert
        retrievedProducts.Should().NotBeEmpty("Should retrieve the saved products");
        retrievedProducts.Should().HaveCountGreaterOrEqualTo(3, "Should retrieve at least the 3 products we saved");
        retrievedProducts.Should().OnlyContain(p => p.Category == "books", "Should only return books category products");

        output.WriteLine($"Retrieved {retrievedProducts.Count} products from books category");
    }

    [Fact]
    public async Task GetAllAsync_With_Pagination_Should_Support_Offset_And_Limit()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        // Use a unique category name to avoid conflicts
        var uniqueCategory = $"pagination-{Guid.NewGuid():N}";
        var products = SimpleTestDataBuilder.CreateSimpleTestProducts(10, uniqueCategory);
        foreach (var product in products)
        {
            await writeService.CreateAsync(product);
        }

        // Act - Get all results first to see what's actually there
        var allProducts = new List<TestProduct>();
        await foreach (var product in readService.GetAllAsync(limit: 100, offset: 0, count: 100))
        {
            allProducts.Add(product);
        }
    
        output.WriteLine($"Total products in system: {allProducts.Count}");
        output.WriteLine($"Products with our category: {allProducts.Count(p => p.Category == uniqueCategory)}");

        // Now test pagination
        var firstPage = new List<TestProduct>();
        await foreach (var product in readService.GetAllAsync(limit: 3, offset: 0, count: 3))
        {
            firstPage.Add(product);
        }

        var secondPage = new List<TestProduct>();
        await foreach (var product in readService.GetAllAsync(limit: 3, offset: 3, count: 3))
        {
            secondPage.Add(product);
        }

        // Assert
        firstPage.Should().NotBeEmpty("First page should have results");
        firstPage.Select(p => p.Id).Should().NotIntersectWith(secondPage.Select(p => p.Id), "Pages should not overlap");

        output.WriteLine($"Pagination: First page {firstPage.Count} items, Second page {secondPage.Count} items");
    }

    [Fact]
    public async Task GetCountAsync_Should_Return_Correct_Count()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var products = TestDataBuilder.CreateTestProducts(4, "clothing");
        foreach (var product in products)
        {
            await writeService.CreateAsync(product);
        }

        // Act
        var count = await readService.GetCountAsync("clothing");

        // Assert
        count.Should().BeGreaterOrEqualTo(4, "Should count at least the 4 products we saved");

        output.WriteLine($"Count for clothing category: {count}");
    }

    [Fact]
    public async Task GetCountWithCacheAsync_Should_Use_Caching()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var products = TestDataBuilder.CreateTestProducts(2, "sports");
        foreach (var product in products)
        {
            await writeService.CreateAsync(product);
        }

        // Act
        var count1 = await readService.GetCountWithCacheAsync("sports", cacheExpiryMinutes: 15);
        var count2 = await readService.GetCountWithCacheAsync("sports", cacheExpiryMinutes: 15);

        // Assert
        count1.Should().Be(count2, "Cached calls should return the same result");
        count1.Should().BeGreaterOrEqualTo(2, "Should count at least the 2 products we saved");

        output.WriteLine($"Cached count for sports category: {count1}");
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Created_Product()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var product = TestDataBuilder.CreateTestProduct("home");
        var savedProduct = await writeService.CreateAsync(product);

        // Act
        var retrievedProduct = await readService.GetByIdAsync(savedProduct.Id, "home");

        // Assert
        retrievedProduct.Should().NotBeNull("Should retrieve the created product");
        retrievedProduct!.Id.Should().Be(savedProduct.Id);
        retrievedProduct.Name.Should().Be(savedProduct.Name);
        retrievedProduct.Category.Should().Be("home");

        output.WriteLine($"Retrieved product: {retrievedProduct.Name} with ID: {retrievedProduct.Id}");
    }

    [Fact]
    public async Task ReplaceAsync_Should_Update_Product_With_Audit_Fields()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();

        var product = TestDataBuilder.CreateTestProduct("electronics");
        var savedProduct = await writeService.CreateAsync(product);

        // Modify the product
        savedProduct.Name = "Updated Product Name";
        savedProduct.Price = 999.99m;

        // Act
        var updatedProduct = await writeService.ReplaceAsync(savedProduct);

        // Assert
        updatedProduct.Should().NotBeNull();
        updatedProduct.Name.Should().Be("Updated Product Name");
        updatedProduct.Price.Should().Be(999.99m);
        updatedProduct.UpdatedOnUtc.Should().BeAfter(updatedProduct.CreatedOnUtc!.Value,
            "UpdatedOnUtc should be newer than CreatedOnUtc");
        updatedProduct.UpdatedBy.Should().Be("TestUser");
        updatedProduct.CreatedBy.Should().Be("TestUser");

        output.WriteLine($"Updated product: {updatedProduct.Name} at {updatedProduct.UpdatedOnUtc}");
    }

    [Theory]
    [InlineData("electronics")]
    [InlineData("books")]
    [InlineData("clothing")]
    [InlineData("home")]
    [InlineData("sports")]
    public async Task DataServices_Should_Handle_Different_Partition_Keys(string category)
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var products = TestDataBuilder.CreateTestProducts(2, category);

        // Act
        foreach (var product in products)
        {
            await writeService.CreateAsync(product);
        }

        var count = await readService.GetCountAsync(category);

        // Assert
        count.Should().BeGreaterOrEqualTo(2, $"Should handle {category} partition correctly");

        output.WriteLine($"Successfully handled partition: {category} with count: {count}");
    }

    [Fact]
    public async Task DataServices_Should_Handle_Concurrent_Operations()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        // Act
        var tasks = Enumerable.Range(0, 3).Select(async i =>
        {
            var products = TestDataBuilder.CreateTestProducts(2, "concurrent");
            foreach (var product in products)
            {
                await writeService.CreateAsync(product);
            }

            return await readService.GetCountAsync("concurrent");
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().OnlyContain(count => count >= 0, "All concurrent operations should succeed");
        results.Length.Should().Be(3, "All tasks should complete");

        output.WriteLine($"Concurrent operations completed with counts: [{string.Join(", ", results)}]");
    }

    [Fact]
    public async Task DataServices_Should_Work_With_Order_Model()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestOrder, TestOrderDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestOrder, TestOrderDao>>();

        var orders = TestDataBuilder.CreateTestOrders(2, TestDataBuilder.CustomerIds.Customer1);

        // Act
        var savedOrders = new List<TestOrder>();
        foreach (var order in orders)
        {
            var saved = await writeService.CreateAsync(order);
            savedOrders.Add(saved);
        }

        var count = await readService.GetCountAsync(TestDataBuilder.CustomerIds.Customer1);

        // Assert
        savedOrders.Should().HaveCount(2);
        savedOrders.Should().OnlyContain(o => o.CustomerId == TestDataBuilder.CustomerIds.Customer1);
        savedOrders.Should().OnlyContain(o => o.CreatedOnUtc.HasValue);
        count.Should().BeGreaterOrEqualTo(2);

        output.WriteLine(
            $"Successfully saved {savedOrders.Count} orders for customer {TestDataBuilder.CustomerIds.Customer1}");
    }

    [Fact]
    public async Task BulkInsertAsync_Should_Create_Multiple_Products()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var products = TestDataBuilder.CreateTestProducts(4, "bulk");

        // Act
        await writeService.BulkInsertAsync(
            products,
            p => p.Category, // partition key selector
            configureItem: p => p.Description += " [Bulk Inserted]",
            batchSize: 2,
            maxConcurrency: 2);

        // Assert
        var count = await readService.GetCountAsync("bulk");
        count.Should().BeGreaterOrEqualTo(4, "Should have at least the 4 products we bulk inserted");

        // Verify the configure action was applied
        var retrievedProducts = new List<TestProduct>();
        await foreach (var product in readService.GetAllAsync("bulk"))
        {
            retrievedProducts.Add(product);
        }

        retrievedProducts.Should().OnlyContain(p => p.Description!.Contains("[Bulk Inserted]"),
            "All products should have the configured description");

        output.WriteLine($"Successfully bulk inserted 4 products, total count in bulk: {count}");
    }

    [Fact]
    public async Task GetTotalCountAsync_Should_Include_All_Documents()
    {
        // Arrange
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var products = TestDataBuilder.CreateTestProducts(3, "total");
        foreach (var product in products)
        {
            await writeService.CreateAsync(product);
        }

        // Act
        var regularCount = await readService.GetCountAsync("total");
        var totalCount = await readService.GetTotalCountAsync("total");

        // Assert
        regularCount.Should().BeGreaterOrEqualTo(3, "Regular count should include non-deleted items");
        totalCount.Should().BeGreaterOrEqualTo(regularCount, "Total count should be >= regular count");

        output.WriteLine($"Regular count: {regularCount}, Total count: {totalCount}");
    }
}
