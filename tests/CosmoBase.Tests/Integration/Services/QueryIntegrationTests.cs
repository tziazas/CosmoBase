using CosmoBase.Abstractions.Filters;
using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Tests.Fixtures;
using CosmoBase.Tests.Helpers;
using CosmoBase.Tests.TestModels;
using FluentAssertions;
using Xunit.Abstractions;

namespace CosmoBase.Tests.Integration.Services;

[Collection("CosmoBase")]
public class QueryIntegrationTests(
    CosmoBaseTestFixture fixture,
    ITestOutputHelper output)
    : IClassFixture<CosmoBaseTestFixture>
{
    // ──────────────────────────────────────────────────────────────
    // SqlSpecification / QueryAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_SqlSpecification_Should_Return_Matching_Documents()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"sqlspec-{Guid.NewGuid():N}";
        var products = TestDataBuilder.CreateTestProducts(5, partition);
        foreach (var p in products)
            await writeService.CreateAsync(p);

        var spec = new SqlSpecification<TestProduct>(
            "SELECT * FROM c WHERE c.Category = @cat",
            new Dictionary<string, object> { ["@cat"] = partition });

        var results = new List<TestProduct>();
        await foreach (var item in readService.QueryAsync(spec))
            results.Add(item);

        results.Should().HaveCountGreaterOrEqualTo(5);
        results.Should().OnlyContain(p => p.Category == partition);

        output.WriteLine($"QueryAsync returned {results.Count} items for partition {partition}");
    }

    [Fact]
    public async Task QueryAsync_SqlSpecification_Should_Support_OrderBy()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"sqlorder-{Guid.NewGuid():N}";
        var prices = new[] { 100m, 50m, 200m, 10m, 150m };
        for (var i = 0; i < prices.Length; i++)
        {
            var p = TestDataBuilder.CreateTestProduct(partition);
            p.Price = prices[i];
            await writeService.CreateAsync(p);
        }

        var spec = new SqlSpecification<TestProduct>(
            "SELECT * FROM c WHERE c.Category = @cat ORDER BY c.Price ASC",
            new Dictionary<string, object> { ["@cat"] = partition });

        var results = new List<TestProduct>();
        await foreach (var item in readService.QueryAsync(spec))
            results.Add(item);

        results.Should().HaveCount(5);
        results.Select(p => p.Price).Should().BeInAscendingOrder(
            "ORDER BY Price ASC should return documents sorted by price");

        output.WriteLine($"Ordered query returned prices: {string.Join(", ", results.Select(p => p.Price))}");
    }

    [Fact]
    public async Task QueryAsync_SqlSpecification_Should_Support_Where_Clause_With_Multiple_Conditions()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"sqlmulti-{Guid.NewGuid():N}";

        var expensiveActive = TestDataBuilder.CreateTestProduct(partition);
        expensiveActive.Price = 500m;
        expensiveActive.IsActive = true;
        await writeService.CreateAsync(expensiveActive);

        var cheapActive = TestDataBuilder.CreateTestProduct(partition);
        cheapActive.Price = 10m;
        cheapActive.IsActive = true;
        await writeService.CreateAsync(cheapActive);

        var expensiveInactive = TestDataBuilder.CreateTestProduct(partition);
        expensiveInactive.Price = 500m;
        expensiveInactive.IsActive = false;
        await writeService.CreateAsync(expensiveInactive);

        var spec = new SqlSpecification<TestProduct>(
            "SELECT * FROM c WHERE c.Category = @cat AND c.Price > @minPrice AND c.IsActive = true",
            new Dictionary<string, object> { ["@cat"] = partition, ["@minPrice"] = 100m });

        var results = new List<TestProduct>();
        await foreach (var item in readService.QueryAsync(spec))
            results.Add(item);

        results.Should().HaveCount(1);
        results[0].Price.Should().Be(500m);
        results[0].IsActive.Should().BeTrue();

        output.WriteLine($"Multi-condition query returned {results.Count} items");
    }

    // ──────────────────────────────────────────────────────────────
    // GetAllByPropertyComparisonAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllByPropertyComparisonAsync_Equal_Should_Return_Matching_Documents()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"propcomp-{Guid.NewGuid():N}";
        var sku = $"SKU-{Guid.NewGuid():N}";

        var target = TestDataBuilder.CreateTestProduct(partition);
        target.Sku = sku;
        await writeService.CreateAsync(target);

        for (var i = 0; i < 2; i++)
        {
            var other = TestDataBuilder.CreateTestProduct(partition);
            other.Sku = $"OTHER-{Guid.NewGuid():N}";
            await writeService.CreateAsync(other);
        }

        var filters = new[]
        {
            new PropertyFilter
            {
                PropertyName = "@Sku",
                PropertyValue = sku,
                PropertyComparison = PropertyComparison.Equal
            }
        };

        var results = await readService.GetAllByPropertyComparisonAsync(filters);

        results.Should().HaveCount(1);
        results[0].Sku.Should().Be(sku);

        output.WriteLine($"PropertyComparison Equal: found {results.Count} match(es) for SKU {sku}");
    }

    [Fact]
    public async Task GetAllByPropertyComparisonAsync_GreaterThan_Should_Return_Correct_Documents()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"propgt-{Guid.NewGuid():N}";
        var sku = $"GT-{Guid.NewGuid():N}";

        var expensive = TestDataBuilder.CreateTestProduct(partition);
        expensive.Price = 9999m;
        expensive.Sku = sku;
        await writeService.CreateAsync(expensive);

        var cheap = TestDataBuilder.CreateTestProduct(partition);
        cheap.Price = 1m;
        cheap.Sku = sku;
        await writeService.CreateAsync(cheap);

        var filters = new[]
        {
            new PropertyFilter
            {
                PropertyName = "@Sku",
                PropertyValue = sku,
                PropertyComparison = PropertyComparison.Equal
            },
            new PropertyFilter
            {
                PropertyName = "@Price",
                PropertyValue = 5000m,
                PropertyComparison = PropertyComparison.GreaterThan
            }
        };

        var results = await readService.GetAllByPropertyComparisonAsync(filters);

        results.Should().HaveCount(1);
        results[0].Price.Should().BeGreaterThan(5000m);

        output.WriteLine($"GreaterThan filter: {results.Count} result(s), price = {results[0].Price}");
    }

    [Fact]
    public async Task GetAllByPropertyComparisonAsync_IncludeDeleted_Should_Return_Soft_Deleted()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"propdel-{Guid.NewGuid():N}";
        var sku = $"DEL-{Guid.NewGuid():N}";

        var product = TestDataBuilder.CreateTestProduct(partition);
        product.Sku = sku;
        var saved = await writeService.CreateAsync(product);

        await writeService.DeleteAsync(saved.Id, partition, Abstractions.Enums.DeleteOptions.SoftDelete);

        var filters = new[]
        {
            new PropertyFilter { PropertyName = "@Sku", PropertyValue = sku, PropertyComparison = PropertyComparison.Equal }
        };

        var withoutDeleted = await readService.GetAllByPropertyComparisonAsync(filters, includeDeleted: false);
        var withDeleted = await readService.GetAllByPropertyComparisonAsync(filters, includeDeleted: true);

        withoutDeleted.Should().BeEmpty("soft-deleted item should not appear without includeDeleted");
        withDeleted.Should().HaveCount(1, "soft-deleted item should appear with includeDeleted=true");

        output.WriteLine($"IncludeDeleted: without={withoutDeleted.Count}, with={withDeleted.Count}");
    }

    // ──────────────────────────────────────────────────────────────
    // GetAllByArrayPropertyAsync  (object array, named property)
    // ──────────────────────────────────────────────────────────────

    // GetAllByArrayPropertyAsync generates: ARRAY_CONTAINS(c.Items, { 'Sku': @value })
    // Without partial_match=true, Cosmos DB performs exact element matching, so this
    // never matches OrderItem objects that have more properties than just 'Sku'.
    // This is a known library limitation. Covered at unit level in CosmosValidatorArrayQueryTests.
    // Use QueryAsync with ARRAY_CONTAINS(@array, @value) for scalar arrays instead (see test below).

    // ──────────────────────────────────────────────────────────────
    // SqlSpecification — scalar array containment (ARRAY_CONTAINS)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_Should_Find_Products_With_Matching_Tag_Via_ArrayContains()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"arraytag-{Guid.NewGuid():N}";
        var uniqueTag = $"tag-{Guid.NewGuid():N}";

        var withTag = TestDataBuilder.CreateTestProduct(partition);
        withTag.Tags = [uniqueTag, "other"];
        await writeService.CreateAsync(withTag);

        var withoutTag = TestDataBuilder.CreateTestProduct(partition);
        withoutTag.Tags = ["unrelated"];
        await writeService.CreateAsync(withoutTag);

        var spec = new SqlSpecification<TestProduct>(
            "SELECT * FROM c WHERE c.Category = @cat AND ARRAY_CONTAINS(c.Tags, @tag)",
            new Dictionary<string, object> { ["@cat"] = partition, ["@tag"] = uniqueTag });

        var results = new List<TestProduct>();
        await foreach (var item in readService.QueryAsync(spec))
            results.Add(item);

        results.Should().HaveCount(1);
        results[0].Tags.Should().Contain(uniqueTag);

        output.WriteLine($"ARRAY_CONTAINS query returned {results.Count} product(s) with tag '{uniqueTag}'");
    }

    // ──────────────────────────────────────────────────────────────
    // GetPageWithTokenAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPageWithTokenAsync_First_Page_Should_Return_ContinuationToken_When_More_Exist()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"page-{Guid.NewGuid():N}";
        for (var i = 0; i < 5; i++)
        {
            var p = TestDataBuilder.CreateTestProduct(partition);
            await writeService.CreateAsync(p);
        }

        var spec = new SqlSpecification<TestProduct>(
            "SELECT * FROM c WHERE c.Category = @cat",
            new Dictionary<string, object> { ["@cat"] = partition });

        var (items, token) = await readService.GetPageWithTokenAsync(spec, partition, pageSize: 2);

        items.Should().HaveCount(2, "page size is 2");
        token.Should().NotBeNull("there are 5 items, so a second page must exist");

        output.WriteLine($"First page: {items.Count} items, token: {(token != null ? "present" : "null")}");
    }

    [Fact]
    public async Task GetPageWithTokenAsync_Last_Page_Should_Return_Null_Token()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"page-{Guid.NewGuid():N}";
        for (var i = 0; i < 3; i++)
        {
            var p = TestDataBuilder.CreateTestProduct(partition);
            await writeService.CreateAsync(p);
        }

        var spec = new SqlSpecification<TestProduct>(
            "SELECT * FROM c WHERE c.Category = @cat",
            new Dictionary<string, object> { ["@cat"] = partition });

        string? token = null;
        var allItems = new List<TestProduct>();
        do
        {
            var (items, next) = await readService.GetPageWithTokenAsync(spec, partition, pageSize: 2, continuationToken: token);
            allItems.AddRange(items);
            token = next;
        } while (token != null);

        allItems.Should().HaveCount(3, "all 3 items should be retrieved across pages");

        output.WriteLine($"Paginated through all {allItems.Count} items");
    }

    [Fact]
    public async Task GetPageWithTokenAsync_Pages_Should_Not_Overlap()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"page-{Guid.NewGuid():N}";
        for (var i = 0; i < 6; i++)
        {
            var p = TestDataBuilder.CreateTestProduct(partition);
            await writeService.CreateAsync(p);
        }

        var spec = new SqlSpecification<TestProduct>(
            "SELECT * FROM c WHERE c.Category = @cat",
            new Dictionary<string, object> { ["@cat"] = partition });

        var (page1, token1) = await readService.GetPageWithTokenAsync(spec, partition, pageSize: 2);
        var (page2, token2) = await readService.GetPageWithTokenAsync(spec, partition, pageSize: 2, continuationToken: token1);

        var page1Ids = page1.Select(p => p.Id).ToHashSet();
        var page2Ids = page2.Select(p => p.Id).ToHashSet();

        page1Ids.Should().NotIntersectWith(page2Ids, "consecutive pages must not contain duplicate items");

        output.WriteLine($"Page 1: [{string.Join(", ", page1Ids)}]");
        output.WriteLine($"Page 2: [{string.Join(", ", page2Ids)}]");
    }

    // ──────────────────────────────────────────────────────────────
    // GetPageWithTokenAndCountAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPageWithTokenAndCountAsync_First_Page_Should_Return_TotalCount()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"pagecnt-{Guid.NewGuid():N}";
        for (var i = 0; i < 5; i++)
        {
            var p = TestDataBuilder.CreateTestProduct(partition);
            await writeService.CreateAsync(p);
        }

        var spec = new SqlSpecification<TestProduct>(
            "SELECT * FROM c WHERE c.Category = @cat",
            new Dictionary<string, object> { ["@cat"] = partition });

        var (items, token, totalCount) = await readService.GetPageWithTokenAndCountAsync(spec, partition, pageSize: 3);

        items.Should().HaveCount(3);
        totalCount.Should().Be(5, "TotalCount should reflect all 5 matching items on the first page");
        token.Should().NotBeNull("more pages exist");

        output.WriteLine($"First page: {items.Count} items, total={totalCount}, token={(token != null ? "present" : "null")}");
    }

    [Fact]
    public async Task GetPageWithTokenAndCountAsync_Subsequent_Page_Should_Have_Null_TotalCount()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"pagecnt-{Guid.NewGuid():N}";
        for (var i = 0; i < 4; i++)
        {
            var p = TestDataBuilder.CreateTestProduct(partition);
            await writeService.CreateAsync(p);
        }

        var spec = new SqlSpecification<TestProduct>(
            "SELECT * FROM c WHERE c.Category = @cat",
            new Dictionary<string, object> { ["@cat"] = partition });

        var (_, token, _) = await readService.GetPageWithTokenAndCountAsync(spec, partition, pageSize: 2);
        var (items2, _, count2) = await readService.GetPageWithTokenAndCountAsync(spec, partition, pageSize: 2, continuationToken: token);

        count2.Should().BeNull("TotalCount should only be calculated on the first page (null token)");
        items2.Should().HaveCount(2);

        output.WriteLine($"Second page: {items2.Count} items, TotalCount={count2?.ToString() ?? "null"}");
    }

    // ──────────────────────────────────────────────────────────────
    // BulkReadAsyncEnumerable
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkReadAsyncEnumerable_Should_Stream_All_Documents_In_Batches()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"bulkread-{Guid.NewGuid():N}";
        for (var i = 0; i < 7; i++)
        {
            var p = TestDataBuilder.CreateTestProduct(partition);
            await writeService.CreateAsync(p);
        }

        var spec = new SqlSpecification<TestProduct>(
            "SELECT * FROM c WHERE c.Category = @cat",
            new Dictionary<string, object> { ["@cat"] = partition });

        var batches = new List<List<TestProduct>>();
        await foreach (var batch in readService.BulkReadAsyncEnumerable(spec, partition, batchSize: 3))
            batches.Add(batch);

        batches.Should().NotBeEmpty();
        var allItems = batches.SelectMany(b => b).ToList();
        allItems.Should().HaveCount(7, "all 7 inserted items should be streamed");
        batches.Should().OnlyContain(b => b.Count > 0, "no empty batches should be emitted");

        output.WriteLine($"BulkRead: {batches.Count} batch(es), {allItems.Count} total items");
    }
}
