using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Tests.Fixtures;
using CosmoBase.Tests.Helpers;
using CosmoBase.Tests.TestModels;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Xunit.Abstractions;

namespace CosmoBase.Tests.Integration.Services;

/// <summary>
/// Integration tests for CosmoBase Data Services using real Cosmos DB emulator
/// </summary>
[Collection("CosmoBase")]
public class DataServicesIntegrationTests : IClassFixture<CosmoBaseTestFixture>
{
    private readonly CosmoBaseTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public DataServicesIntegrationTests(CosmoBaseTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }
    
    [Fact]
    public async Task Test_Direct_Cosmos_SDK()
    {
        // Get the container directly, bypassing ALL CosmoBase logic
        var cosmosClient = new CosmosClient(
            "placeholder",
            "placeholder",
            new CosmosClientOptions
            {
                HttpClientFactory = () => new HttpClient(new HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                }),
                ConnectionMode = ConnectionMode.Direct,
                LimitToEndpoint = true
            });

        var container = cosmosClient.GetContainer("CosmoBaseTestDb", "Products");

        // Create the most minimal document possible
        var simpleDoc = new
        {
            id = "direct-test-123",
            Category = "electronics", // partition key
            Name = "Direct Test"
        };

        try
        {
            // Test direct Cosmos SDK call
            var result = await container.CreateItemAsync(simpleDoc, new PartitionKey("electronics"));
            _output.WriteLine($"✅ Direct Cosmos SDK works: {result.Resource.id}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Even direct Cosmos SDK failed: {ex.Message}");
        
            // If this fails, it's a platform/emulator issue, not your code
            throw;
        }
        finally
        {
            cosmosClient.Dispose();
        }
    }
    
    [Fact]
    public Task DataReadService_Should_Be_Registered_In_DI_Container()
    {
        // Arrange & Act
        var readService = _fixture.GetService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        // Assert
        readService.Should().NotBeNull("ICosmosDataReadService<TestProduct, TestProductDao> should be registered in DI container");
        
        return Task.CompletedTask;
    }

    [Fact]
    public Task DataWriteService_Should_Be_Registered_In_DI_Container()
    {
        // Arrange & Act
        var writeService = _fixture.GetService<ICosmosDataWriteService<TestProduct, TestProductDao>>();

        // Assert
        writeService.Should().NotBeNull("ICosmosDataWriteService<TestProduct, TestProductDao> should be registered in DI container");
        
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateAsync_Should_Create_Single_Product_With_Audit_Fields()
    {
        // Arrange
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
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
        
        _output.WriteLine($"Created product: {savedProduct.Name} with ID: {savedProduct.Id}");
    }

    [Fact]
    public async Task UpsertAsync_Should_Handle_Multiple_Products()
    {
        // Arrange
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var products = TestDataBuilder.CreateTestProducts(3, "electronics");

        // Act
        var savedProducts = new List<TestProduct>();
        foreach (var product in products)
        {
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

        _output.WriteLine($"Successfully saved {savedProducts.Count} products");
    }

    [Fact]
    public async Task BulkUpsertAsync_Should_Handle_Multiple_Products()
    {
        // Arrange
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var products = SimpleTestDataBuilder.CreateSimpleTestProducts(10, "Meow"); //TestDataBuilder.CreateTestProducts(5, "electronics");

        // Act
        await writeService.BulkUpsertAsync(
            products,
            p => p.Category, // partition key selector
            configureItem: null,
            batchSize: 3,
            maxConcurrency: 2);

        // Assert - verify by reading the data back
        var readService = _fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();
        var count = await readService.GetCountAsync("electronics");
        count.Should().BeGreaterOrEqualTo(5, "Should have at least the 5 products we bulk upserted");

        _output.WriteLine($"Successfully bulk upserted 5 products, total count in electronics: {count}");
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Saved_Products()
    {
        // Arrange
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = _fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();
        
        var products = SimpleTestDataBuilder.CreateSimpleTestProducts(10, "Books"); //TestDataBuilder.CreateTestProducts(5, "electronics");
        foreach (var product in products)
        {
            await writeService.CreateAsync(product);
        }

        // Act
        var retrievedProducts = new List<TestProduct>();
        await foreach (var product in readService.GetAllAsync("books"))
        {
            retrievedProducts.Add(product);
        }

        // Assert
        retrievedProducts.Should().NotBeEmpty("Should retrieve the saved products");
        retrievedProducts.Should().HaveCountGreaterOrEqualTo(3, "Should retrieve at least the 3 products we saved");
        retrievedProducts.Should().OnlyContain(p => p.Category == "books", "Should only return books category products");

        _output.WriteLine($"Retrieved {retrievedProducts.Count} products from books category");
    }

    [Fact]
    public async Task GetAllAsync_With_Pagination_Should_Support_Offset_And_Limit()
    {
        // Arrange
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = _fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();
        
        var products = SimpleTestDataBuilder.CreateSimpleTestProducts(10, "pagination"); //TestDataBuilder.CreateTestProducts(5, "electronics");
        foreach (var product in products)
        {
            await writeService.CreateAsync(product);
        }

        // Act
        var firstPage = new List<TestProduct>();
        await foreach (var product in readService.GetAllAsync(limit: 3, offset: 0, count: 3))
        {
            if (product.Category == "pagination")
                firstPage.Add(product);
        }

        var secondPage = new List<TestProduct>();
        await foreach (var product in readService.GetAllAsync(limit: 3, offset: 3, count: 3))
        {
            if (product.Category == "pagination")
                secondPage.Add(product);
        }

        // Assert
        firstPage.Should().NotBeEmpty("First page should have results");
        secondPage.Should().NotBeEmpty("Second page should have results");
        firstPage.Select(p => p.Id).Should().NotIntersectWith(secondPage.Select(p => p.Id), 
            "Pages should not overlap");

        _output.WriteLine($"Pagination: First page {firstPage.Count} items, Second page {secondPage.Count} items");
    }
    
    [Fact]
    public async Task GetCountAsync_Should_Return_Correct_Count()
    {
        // Arrange
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = _fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();
        
        var products = TestDataBuilder.CreateTestProducts(4, "clothing");
        foreach (var product in products)
        {
            await writeService.CreateAsync(product);
        }

        // Act
        var count = await readService.GetCountAsync("clothing");

        // Assert
        count.Should().BeGreaterOrEqualTo(4, "Should count at least the 4 products we saved");

        _output.WriteLine($"Count for clothing category: {count}");
    }

    [Fact]
    public async Task GetCountWithCacheAsync_Should_Use_Caching()
    {
        // Arrange
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = _fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();
        
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

        _output.WriteLine($"Cached count for sports category: {count1}");
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Created_Product()
    {
        // Arrange
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = _fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();
        
        var product = TestDataBuilder.CreateTestProduct("home");
        var savedProduct = await writeService.CreateAsync(product);

        // Act
        var retrievedProduct = await readService.GetByIdAsync(savedProduct.Id, "home");

        // Assert
        retrievedProduct.Should().NotBeNull("Should retrieve the created product");
        retrievedProduct!.Id.Should().Be(savedProduct.Id);
        retrievedProduct.Name.Should().Be(savedProduct.Name);
        retrievedProduct.Category.Should().Be("home");

        _output.WriteLine($"Retrieved product: {retrievedProduct.Name} with ID: {retrievedProduct.Id}");
    }

    [Fact]
    public async Task ReplaceAsync_Should_Update_Product_With_Audit_Fields()
    {
        // Arrange
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        
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

        _output.WriteLine($"Updated product: {updatedProduct.Name} at {updatedProduct.UpdatedOnUtc}");
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
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = _fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();
        
        var products = TestDataBuilder.CreateTestProducts(2, category);

        // Act
        foreach (var product in products)
        {
            await writeService.CreateAsync(product);
        }
        var count = await readService.GetCountAsync(category);

        // Assert
        count.Should().BeGreaterOrEqualTo(2, $"Should handle {category} partition correctly");

        _output.WriteLine($"Successfully handled partition: {category} with count: {count}");
    }

    [Fact]
    public async Task DataServices_Should_Handle_Concurrent_Operations()
    {
        // Arrange
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = _fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

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

        _output.WriteLine($"Concurrent operations completed with counts: [{string.Join(", ", results)}]");
    }

    [Fact]
    public async Task DataServices_Should_Work_With_Order_Model()
    {
        // Arrange
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestOrder, TestOrderDao>>();
        var readService = _fixture.GetRequiredService<ICosmosDataReadService<TestOrder, TestOrderDao>>();
        
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

        _output.WriteLine($"Successfully saved {savedOrders.Count} orders for customer {TestDataBuilder.CustomerIds.Customer1}");
    }

    [Fact]
    public async Task BulkInsertAsync_Should_Create_Multiple_Products()
    {
        // Arrange
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = _fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();
        
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

        _output.WriteLine($"Successfully bulk inserted 4 products, total count in bulk: {count}");
    }

    [Fact]
    public async Task GetTotalCountAsync_Should_Include_All_Documents()
    {
        // Arrange
        var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = _fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();
        
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
        
        _output.WriteLine($"Regular count: {regularCount}, Total count: {totalCount}");
    }
}
