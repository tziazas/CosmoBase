using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Tests.Fixtures;
using CosmoBase.Tests.Helpers;
using CosmoBase.Tests.TestModels;
using FluentAssertions;
using Xunit.Abstractions;

namespace CosmoBase.Tests.Integration.Services;

[Collection("CosmoBase")]
public class SoftDeleteIntegrationTests(
    CosmoBaseTestFixture fixture,
    ITestOutputHelper output)
    : IClassFixture<CosmoBaseTestFixture>
{
    [Fact]
    public async Task SoftDelete_Should_Hide_Document_From_Default_GetAllAsync()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"softdel-{Guid.NewGuid():N}";
        var product = TestDataBuilder.CreateTestProduct(partition);
        var saved = await writeService.CreateAsync(product);

        await writeService.DeleteAsync(saved.Id, partition, DeleteOptions.SoftDelete);

        var items = new List<TestProduct>();
        await foreach (var p in readService.GetAllAsync(partition))
            items.Add(p);

        items.Should().NotContain(p => p.Id == saved.Id,
            "soft-deleted document should not appear in default GetAllAsync");

        output.WriteLine($"Soft-deleted {saved.Id}; partition {partition} has {items.Count} visible items");
    }

    [Fact]
    public async Task SoftDelete_Should_Be_Visible_With_IncludeDeleted_On_GetByIdAsync()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"softdel-{Guid.NewGuid():N}";
        var product = TestDataBuilder.CreateTestProduct(partition);
        var saved = await writeService.CreateAsync(product);

        await writeService.DeleteAsync(saved.Id, partition, DeleteOptions.SoftDelete);

        var visible = await readService.GetByIdAsync(saved.Id, partition, includeDeleted: false);
        var withDeleted = await readService.GetByIdAsync(saved.Id, partition, includeDeleted: true);

        visible.Should().BeNull("GetByIdAsync should exclude soft-deleted by default");
        withDeleted.Should().NotBeNull("GetByIdAsync with includeDeleted=true should return soft-deleted doc");
        withDeleted!.Deleted.Should().BeTrue();

        output.WriteLine($"Verified soft-delete visibility for {saved.Id}");
    }

    [Fact]
    public async Task SoftDelete_Should_Set_Audit_Fields()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"softdel-{Guid.NewGuid():N}";
        var product = TestDataBuilder.CreateTestProduct(partition);
        var saved = await writeService.CreateAsync(product);
        var createdAt = saved.UpdatedOnUtc;

        await Task.Delay(10);
        await writeService.DeleteAsync(saved.Id, partition, DeleteOptions.SoftDelete);

        var deleted = await readService.GetByIdAsync(saved.Id, partition, includeDeleted: true);

        deleted.Should().NotBeNull();
        deleted!.Deleted.Should().BeTrue();
        deleted.UpdatedBy.Should().Be("TestUser");
        deleted.UpdatedOnUtc.Should().BeAfter(createdAt!.Value,
            "UpdatedOnUtc should advance when soft-deleted");

        output.WriteLine($"Soft-delete audit fields verified for {saved.Id}");
    }

    [Fact]
    public async Task SoftDelete_Should_Reduce_GetCountAsync()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"softdel-{Guid.NewGuid():N}";
        var products = TestDataBuilder.CreateTestProducts(3, partition);
        foreach (var p in products)
            await writeService.CreateAsync(p);

        var beforeCount = await readService.GetCountAsync(partition);

        var first = (await readService.GetByIdAsync(products[0].Id, partition))!;
        await writeService.DeleteAsync(first.Id, partition, DeleteOptions.SoftDelete);

        var afterCount = await readService.GetCountAsync(partition);

        afterCount.Should().Be(beforeCount - 1,
            "GetCountAsync should exclude soft-deleted documents");

        output.WriteLine($"Count before: {beforeCount}, after soft delete: {afterCount}");
    }

    [Fact]
    public async Task GetTotalCountAsync_Should_Include_Soft_Deleted_Documents()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"softdel-{Guid.NewGuid():N}";
        var products = TestDataBuilder.CreateTestProducts(3, partition);
        foreach (var p in products)
            await writeService.CreateAsync(p);

        var first = (await readService.GetByIdAsync(products[0].Id, partition))!;
        await writeService.DeleteAsync(first.Id, partition, DeleteOptions.SoftDelete);

        var regular = await readService.GetCountAsync(partition);
        var total = await readService.GetTotalCountAsync(partition);

        total.Should().Be(regular + 1,
            "GetTotalCountAsync should include soft-deleted while GetCountAsync excludes them");

        output.WriteLine($"Regular: {regular}, Total: {total}");
    }

    [Fact]
    public async Task HardDelete_Should_Remove_Document_Permanently()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"harddel-{Guid.NewGuid():N}";
        var product = TestDataBuilder.CreateTestProduct(partition);
        var saved = await writeService.CreateAsync(product);

        await writeService.DeleteAsync(saved.Id, partition, DeleteOptions.HardDelete);

        var retrieved = await readService.GetByIdAsync(saved.Id, partition, includeDeleted: true);

        retrieved.Should().BeNull("hard-deleted document must not be retrievable");

        output.WriteLine($"Hard-deleted {saved.Id}; confirmed not retrievable");
    }

    [Fact]
    public async Task HardDelete_Should_Reduce_Both_CountAsync_And_TotalCountAsync()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"harddel-{Guid.NewGuid():N}";
        var products = TestDataBuilder.CreateTestProducts(2, partition);
        foreach (var p in products)
            await writeService.CreateAsync(p);

        var before = await readService.GetCountAsync(partition);
        var beforeTotal = await readService.GetTotalCountAsync(partition);

        var target = (await readService.GetByIdAsync(products[0].Id, partition))!;
        await writeService.DeleteAsync(target.Id, partition, DeleteOptions.HardDelete);

        var after = await readService.GetCountAsync(partition);
        var afterTotal = await readService.GetTotalCountAsync(partition);

        after.Should().Be(before - 1);
        afterTotal.Should().Be(beforeTotal - 1);

        output.WriteLine($"Hard delete: count {before}→{after}, total {beforeTotal}→{afterTotal}");
    }
}
