using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Abstractions.Enums;
using ConsoleApp.Basic.Models;
using CosmoBase.Abstractions.Filters;
using Microsoft.Extensions.Logging;

namespace ConsoleApp.Basic.Services;

public class ProductService
{
    private readonly ICosmosDataReadService<Product, ProductDao> _readService;
    private readonly ICosmosDataWriteService<Product, ProductDao> _writeService;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        ICosmosDataReadService<Product, ProductDao> readService,
        ICosmosDataWriteService<Product, ProductDao> writeService,
        ILogger<ProductService> logger)
    {
        _readService = readService;
        _writeService = writeService;
        _logger = logger;
    }

    public async Task DemonstrateProductOperationsAsync()
    {
        Console.WriteLine("1️⃣ Creating products...");

        // Create products
        var laptop = new Product
        {
            Name = "Gaming Laptop",
            Category = "electronics",
            Price = 1299.99m,
            Description = "High-performance gaming laptop",
            Tags = ["gaming", "laptop", "performance"],
            StockQuantity = 10,
            Sku = "LAPTOP001"
        };

        var mouse = new Product
        {
            Name = "Wireless Mouse",
            Category = "electronics", 
            Price = 49.99m,
            Description = "Ergonomic wireless mouse",
            Tags = ["mouse", "wireless", "ergonomic"],
            StockQuantity = 50,
            Sku = "MOUSE001"
        };

        var createdLaptop = await _writeService.CreateAsync(laptop);
        var createdMouse = await _writeService.CreateAsync(mouse);

        Console.WriteLine($"✅ Created laptop: {createdLaptop.Name} (ID: {createdLaptop.Id})");
        Console.WriteLine($"   📅 Created: {createdLaptop.CreatedOnUtc} by {createdLaptop.CreatedBy}");
        Console.WriteLine($"✅ Created mouse: {createdMouse.Name} (ID: {createdMouse.Id})");

        Console.WriteLine("\n2️⃣ Reading products...");

        // Read products back
        var retrievedLaptop = await _readService.GetByIdAsync(createdLaptop.Id, "electronics");
        Console.WriteLine($"📖 Retrieved: {retrievedLaptop?.Name} - ${retrievedLaptop?.Price}");

        Console.WriteLine("\n3️⃣ Updating product...");

        // Update product
        createdLaptop.Price = 1199.99m;
        createdLaptop.Description = "High-performance gaming laptop - ON SALE!";
        var updatedLaptop = await _writeService.ReplaceAsync(createdLaptop);

        Console.WriteLine($"🔄 Updated laptop price: ${updatedLaptop.Price}");
        Console.WriteLine($"   📅 Updated: {updatedLaptop.UpdatedOnUtc} by {updatedLaptop.UpdatedBy}");
        Console.WriteLine($"   📅 Originally created: {updatedLaptop.CreatedOnUtc} by {updatedLaptop.CreatedBy}");

        Console.WriteLine("\n4️⃣ Upserting product...");

        // Upsert (create or update)
        var keyboard = new Product
        {
            Name = "Mechanical Keyboard",
            Category = "electronics",
            Price = 129.99m,
            Description = "RGB mechanical keyboard",
            Tags = ["keyboard", "mechanical", "rgb"],
            StockQuantity = 25,
            Sku = "KEYBOARD001"
        };

        var upsertedKeyboard = await _writeService.UpsertAsync(keyboard);
        Console.WriteLine($"🔄 Upserted keyboard: {upsertedKeyboard.Name} (ID: {upsertedKeyboard.Id})");
    }

    public async Task DemonstrateBulkOperationsAsync()
    {
        Console.WriteLine("1️⃣ Bulk inserting products...");

        var bulkProducts = new List<Product>();
        var categories = new[] { "electronics", "books", "clothing" };

        for (int i = 1; i <= 15; i++)
        {
            var category = categories[i % categories.Length];
            bulkProducts.Add(new Product
            {
                Name = $"Bulk Product {i}",
                Category = category,
                Price = 10.00m + i,
                Description = $"Bulk inserted product number {i}",
                Tags = ["bulk", "demo", category],
                StockQuantity = i * 2,
                Sku = $"BULK{i:D3}"
            });
        }

        // Group by category for bulk operations (same partition key required)
        var productsByCategory = bulkProducts.GroupBy(p => p.Category);

        foreach (var group in productsByCategory)
        {
            await _writeService.BulkInsertAsync(
                group,
                p => p.Category, // partition key selector
                configureItem: p => p.Description += " [Bulk Inserted]",
                batchSize: 5,
                maxConcurrency: 2);

            Console.WriteLine($"✅ Bulk inserted {group.Count()} products in {group.Key} category");
        }

        Console.WriteLine("\n2️⃣ Bulk upserting products...");

        // Modify some products and bulk upsert
        var electronicsProducts = bulkProducts.Where(p => p.Category == "electronics").ToList();
        foreach (var product in electronicsProducts)
        {
            product.Price += 5.00m; // Increase price
            product.Description += " [Updated via Bulk Upsert]";
        }

        await _writeService.BulkUpsertAsync(
            electronicsProducts,
            p => p.Category,
            configureItem: null,
            batchSize: 3,
            maxConcurrency: 2);

        Console.WriteLine($"✅ Bulk upserted {electronicsProducts.Count} electronics products");
    }

    public async Task DemonstrateQueryOperationsAsync()
    {
        Console.WriteLine("1️⃣ Streaming all electronics products...");

        var electronicsCount = 0;
        await foreach (var product in _readService.GetAllAsync("electronics"))
        {
            electronicsCount++;
            if (electronicsCount <= 5) // Show first 5
            {
                Console.WriteLine($"   📦 {product.Name} - ${product.Price} (Stock: {product.StockQuantity})");
            }
        }
        Console.WriteLine($"📊 Total electronics products: {electronicsCount}");

        Console.WriteLine("\n2️⃣ Getting count with caching...");

        // Get count with 5-minute cache
        var cachedCount = await _readService.GetCountWithCacheAsync("electronics", cacheExpiryMinutes: 5);
        Console.WriteLine($"📊 Cached count for electronics: {cachedCount}");

        // Get count without cache
        var freshCount = await _readService.GetCountWithCacheAsync("electronics", cacheExpiryMinutes: 0);
        Console.WriteLine($"📊 Fresh count for electronics: {freshCount}");

        Console.WriteLine("\n3️⃣ Paginated retrieval...");

        // Get first page
        var (firstPage, continuationToken, totalCount) = await _readService.GetPageWithTokenAndCountAsync(
            new SqlSpecification<Product>("SELECT * FROM c WHERE c.Category = 'electronics' ORDER BY c.Name"),
            "electronics",
            pageSize: 3);

        Console.WriteLine($"📄 First page: {firstPage.Count} items (Total: {totalCount})");
        foreach (var product in firstPage)
        {
            Console.WriteLine($"   📦 {product.Name} - ${product.Price}");
        }

        if (!string.IsNullOrEmpty(continuationToken))
        {
            // Get second page
            var (secondPage, _, _) = await _readService.GetPageWithTokenAndCountAsync(
                new SqlSpecification<Product>("SELECT * FROM c WHERE c.Category = 'electronics' ORDER BY c.Name"),
                "electronics",
                pageSize: 3,
                continuationToken);

            Console.WriteLine($"📄 Second page: {secondPage.Count} items");
            foreach (var product in secondPage)
            {
                Console.WriteLine($"   📦 {product.Name} - ${product.Price}");
            }
        }
    }

    public async Task DemonstrateDeleteOperationsAsync()
    {
        Console.WriteLine("1️⃣ Creating product for deletion demo...");

        var tempProduct = new Product
        {
            Name = "Temporary Product",
            Category = "electronics",
            Price = 1.00m,
            Description = "This product will be deleted"
        };

        var created = await _writeService.CreateAsync(tempProduct);
        Console.WriteLine($"✅ Created temporary product: {created.Id}");

        Console.WriteLine("\n2️⃣ Soft delete...");

        // Soft delete (marks as deleted but keeps the record)
        await _writeService.DeleteAsync(created.Id, "electronics", DeleteOptions.SoftDelete);
        Console.WriteLine($"🗑️ Soft deleted product: {created.Id}");

        // Try to retrieve (should return null for soft-deleted)
        var retrievedAfterSoftDelete = await _readService.GetByIdAsync(created.Id, "electronics", includeDeleted: false);
        Console.WriteLine($"📖 Retrieved after soft delete (includeDeleted=false): {(retrievedAfterSoftDelete == null ? "null" : "found")}");

        // Try to retrieve with includeDeleted=true
        var retrievedIncludeDeleted = await _readService.GetByIdAsync(created.Id, "electronics", includeDeleted: true);
        Console.WriteLine($"📖 Retrieved with includeDeleted=true: {(retrievedIncludeDeleted == null ? "null" : "found")}");

        Console.WriteLine("\n3️⃣ Hard delete...");

        // Hard delete (permanently removes)
        await _writeService.DeleteAsync(created.Id, "electronics", DeleteOptions.HardDelete);
        Console.WriteLine($"🗑️ Hard deleted product: {created.Id}");

        // Try to retrieve (should return null)
        var retrievedAfterHardDelete = await _readService.GetByIdAsync(created.Id, "electronics", includeDeleted: true);
        Console.WriteLine($"📖 Retrieved after hard delete: {(retrievedAfterHardDelete == null ? "null" : "found")}");
    }
}