using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Exceptions;
using CosmoBase.Abstractions.Filters;
using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Tests.Fixtures;
using CosmoBase.Tests.Helpers;
using CosmoBase.Tests.TestModels;
using FluentAssertions;
using Xunit.Abstractions;

namespace CosmoBase.Tests.Integration.Services;

[Collection("CosmoBase")]
public class PatchIntegrationTests(
    CosmoBaseTestFixture fixture,
    ITestOutputHelper output)
    : IClassFixture<CosmoBaseTestFixture>
{
    [Fact]
    public async Task PatchDocumentAsync_Replace_Should_Update_Single_Field()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"patch-{Guid.NewGuid():N}";
        var product = TestDataBuilder.CreateTestProduct(partition);
        product.Price = 100m;
        var saved = await writeService.CreateAsync(product);

        var patchSpec = new PatchSpecification(
        [
            new PatchOperationSpecification("/Price", PatchOperationType.Replace, 999m)
        ]);

        var patched = await writeService.PatchDocumentAsync(saved.Id, partition, patchSpec);

        patched.Should().NotBeNull();
        patched!.Price.Should().Be(999m, "Replace operation should update Price to 999");

        var retrieved = await readService.GetByIdAsync(saved.Id, partition);
        retrieved!.Price.Should().Be(999m, "change should be persisted in Cosmos");

        output.WriteLine($"Patched price: {saved.Price} → {patched.Price}");
    }

    [Fact]
    public async Task PatchDocumentAsync_Replace_Multiple_Fields_Should_Update_All()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"patch-{Guid.NewGuid():N}";
        var product = TestDataBuilder.CreateTestProduct(partition);
        product.Price = 50m;
        product.IsActive = true;
        var saved = await writeService.CreateAsync(product);

        var patchSpec = new PatchSpecification(
        [
            new PatchOperationSpecification("/Price", PatchOperationType.Replace, 250m),
            new PatchOperationSpecification("/IsActive", PatchOperationType.Replace, false)
        ]);

        var patched = await writeService.PatchDocumentAsync(saved.Id, partition, patchSpec);

        patched.Should().NotBeNull();
        patched!.Price.Should().Be(250m);
        patched.IsActive.Should().BeFalse();

        output.WriteLine($"Multi-field patch: price={patched.Price}, isActive={patched.IsActive}");
    }

    [Fact]
    public async Task PatchDocumentAsync_Add_Should_Set_Nullable_Field()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"patch-{Guid.NewGuid():N}";
        var product = TestDataBuilder.CreateTestProduct(partition);
        product.Barcode = null;
        var saved = await writeService.CreateAsync(product);

        var newBarcode = $"BC-{Guid.NewGuid():N}";
        var patchSpec = new PatchSpecification(
        [
            new PatchOperationSpecification("/Barcode", PatchOperationType.Add, newBarcode)
        ]);

        var patched = await writeService.PatchDocumentAsync(saved.Id, partition, patchSpec);

        patched.Should().NotBeNull();
        patched!.Barcode.Should().Be(newBarcode, "Add should set a previously-null nullable field");

        output.WriteLine($"Add patch set Barcode = {patched.Barcode}");
    }

    [Fact]
    public async Task PatchDocumentAsync_Remove_Should_Clear_Nullable_Field()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"patch-{Guid.NewGuid():N}";
        var product = TestDataBuilder.CreateTestProduct(partition);
        product.Barcode = "TO-REMOVE";
        var saved = await writeService.CreateAsync(product);

        var patchSpec = new PatchSpecification(
        [
            new PatchOperationSpecification("/Barcode", PatchOperationType.Remove)
        ]);

        var patched = await writeService.PatchDocumentAsync(saved.Id, partition, patchSpec);

        patched.Should().NotBeNull();
        patched!.Barcode.Should().BeNull("Remove should clear the Barcode field");

        output.WriteLine($"Remove patch cleared Barcode; result = {patched.Barcode?.ToString() ?? "null"}");
    }

    [Fact]
    public async Task PatchDocumentAsync_Set_Should_Throw_NotSupportedException()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();

        var partition = $"patch-{Guid.NewGuid():N}";
        var product = TestDataBuilder.CreateTestProduct(partition);
        var saved = await writeService.CreateAsync(product);

        var patchSpec = new PatchSpecification(
        [
            new PatchOperationSpecification("/Price", PatchOperationType.Set, 42m)
        ]);

        var act = () => writeService.PatchDocumentAsync(saved.Id, partition, patchSpec);

        // NotSupportedException is thrown inside PatchSpecificationExtensions and wrapped
        // by the repository in a CosmoBaseException so callers get a consistent exception type.
        var ex = await act.Should().ThrowAsync<CosmoBaseException>(
            "PatchOperationType.Set is explicitly unsupported and the repo wraps it in CosmoBaseException");
        ex.WithInnerException<NotSupportedException>();

        output.WriteLine("Verified Set operation throws CosmoBaseException(NotSupportedException)");
    }

    [Fact]
    public async Task PatchDocumentAsync_Increment_Should_Throw_NotSupportedException()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();

        var partition = $"patch-{Guid.NewGuid():N}";
        var product = TestDataBuilder.CreateTestProduct(partition);
        var saved = await writeService.CreateAsync(product);

        var patchSpec = new PatchSpecification(
        [
            new PatchOperationSpecification("/StockQuantity", PatchOperationType.Increment, 5)
        ]);

        var act = () => writeService.PatchDocumentAsync(saved.Id, partition, patchSpec);

        var ex = await act.Should().ThrowAsync<CosmoBaseException>(
            "PatchOperationType.Increment is explicitly unsupported and the repo wraps it in CosmoBaseException");
        ex.WithInnerException<NotSupportedException>();

        output.WriteLine("Verified Increment operation throws CosmoBaseException(NotSupportedException)");
    }

    [Fact]
    public async Task UpsertAsync_New_Document_Should_Set_Create_Audit_Fields()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();

        var partition = $"upsert-{Guid.NewGuid():N}";
        var product = TestDataBuilder.CreateTestProduct(partition);
        product.CreatedOnUtc = null;

        var saved = await writeService.UpsertAsync(product);

        saved.CreatedOnUtc.Should().NotBeNull("upsert of new doc must set CreatedOnUtc");
        saved.CreatedBy.Should().Be("TestUser");
        saved.UpdatedOnUtc.Should().NotBeNull();
        saved.UpdatedBy.Should().Be("TestUser");

        output.WriteLine($"Upsert (new): CreatedOnUtc={saved.CreatedOnUtc}, CreatedBy={saved.CreatedBy}");
    }

    [Fact]
    public async Task UpsertAsync_Existing_Document_Should_Preserve_Created_Audit_Fields()
    {
        var writeService = fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
        var readService = fixture.GetRequiredService<ICosmosDataReadService<TestProduct, TestProductDao>>();

        var partition = $"upsert-{Guid.NewGuid():N}";
        var product = TestDataBuilder.CreateTestProduct(partition);
        var created = await writeService.CreateAsync(product);

        var originalCreatedOn = created.CreatedOnUtc;
        var originalCreatedBy = created.CreatedBy;

        created.Price = 777m;
        await Task.Delay(10);
        var upserted = await writeService.UpsertAsync(created);

        upserted.CreatedOnUtc.Should().Be(originalCreatedOn,
            "CreatedOnUtc must not change on upsert of existing document");
        upserted.CreatedBy.Should().Be(originalCreatedBy,
            "CreatedBy must not change on upsert of existing document");
        upserted.UpdatedOnUtc.Should().BeAfter(originalCreatedOn!.Value,
            "UpdatedOnUtc should advance");

        output.WriteLine($"Upsert (existing): CreatedOnUtc preserved={upserted.CreatedOnUtc == originalCreatedOn}");
    }
}
