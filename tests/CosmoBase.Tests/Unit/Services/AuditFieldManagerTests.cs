using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Core.Services;
using CosmoBase.Tests.TestModels;
using FluentAssertions;
using Moq;

namespace CosmoBase.Tests.Unit.Services;

public class AuditFieldManagerTests
{
    private static AuditFieldManager<TestProductDao> BuildManager(string user = "alice")
    {
        var ctx = new Mock<IUserContext>();
        ctx.Setup(c => c.GetCurrentUser()).Returns(user);
        return new AuditFieldManager<TestProductDao>(ctx.Object);
    }

    // ── SetCreateAuditFields ────────────────────────────────────────

    [Fact]
    public void SetCreateAuditFields_Should_Set_All_Five_Fields()
    {
        var mgr = BuildManager("alice");
        var dao = new TestProductDao();

        mgr.SetCreateAuditFields(dao);

        dao.CreatedOnUtc.Should().NotBeNull();
        dao.UpdatedOnUtc.Should().NotBeNull();
        dao.CreatedBy.Should().Be("alice");
        dao.UpdatedBy.Should().Be("alice");
        dao.Deleted.Should().BeFalse();
    }

    [Fact]
    public void SetCreateAuditFields_CreatedOnUtc_And_UpdatedOnUtc_Should_Be_Equal()
    {
        var mgr = BuildManager();
        var dao = new TestProductDao();

        mgr.SetCreateAuditFields(dao);

        dao.CreatedOnUtc.Should().Be(dao.UpdatedOnUtc,
            "both timestamps are captured from the same DateTime.UtcNow call");
    }

    [Fact]
    public void SetCreateAuditFields_Should_Overwrite_Existing_Audit_Values()
    {
        var mgr = BuildManager("new-user");
        var dao = new TestProductDao
        {
            CreatedBy = "old-user",
            CreatedOnUtc = DateTime.UtcNow.AddDays(-10),
            Deleted = true
        };

        mgr.SetCreateAuditFields(dao);

        dao.CreatedBy.Should().Be("new-user");
        dao.Deleted.Should().BeFalse();
    }

    // ── SetUpdateAuditFields ───────────────────────────────────────

    [Fact]
    public void SetUpdateAuditFields_Should_Set_Updated_Fields()
    {
        var mgr = BuildManager("bob");
        var dao = new TestProductDao
        {
            CreatedOnUtc = DateTime.UtcNow.AddMinutes(-5),
            CreatedBy = "original"
        };

        mgr.SetUpdateAuditFields(dao);

        dao.UpdatedOnUtc.Should().NotBeNull();
        dao.UpdatedBy.Should().Be("bob");
    }

    [Fact]
    public void SetUpdateAuditFields_Should_Preserve_CreatedOnUtc_When_Already_Set()
    {
        var mgr = BuildManager("bob");
        var originalCreated = DateTime.UtcNow.AddHours(-1);
        var dao = new TestProductDao
        {
            CreatedOnUtc = originalCreated,
            CreatedBy = "original"
        };

        mgr.SetUpdateAuditFields(dao);

        dao.CreatedOnUtc.Should().Be(originalCreated, "update must not change CreatedOnUtc");
        dao.CreatedBy.Should().Be("original", "update must not change CreatedBy");
    }

    [Fact]
    public void SetUpdateAuditFields_Should_Set_CreatedOnUtc_When_Missing()
    {
        var mgr = BuildManager("carol");
        var dao = new TestProductDao { CreatedOnUtc = null };

        mgr.SetUpdateAuditFields(dao);

        dao.CreatedOnUtc.Should().NotBeNull(
            "SetUpdateAuditFields must backfill CreatedOnUtc when it is absent");
        dao.CreatedBy.Should().Be("carol");
    }

    // ── SetUpsertAuditFields ───────────────────────────────────────

    [Fact]
    public void SetUpsertAuditFields_New_Document_Null_Created_Should_Set_All_Fields()
    {
        var mgr = BuildManager("dave");
        var dao = new TestProductDao { CreatedOnUtc = null };

        mgr.SetUpsertAuditFields(dao);

        dao.CreatedOnUtc.Should().NotBeNull();
        dao.CreatedBy.Should().Be("dave");
        dao.UpdatedOnUtc.Should().NotBeNull();
        dao.UpdatedBy.Should().Be("dave");
        dao.Deleted.Should().BeFalse();
    }

    [Fact]
    public void SetUpsertAuditFields_Existing_Document_Should_Preserve_Created_Fields()
    {
        var mgr = BuildManager("eve");
        var originalCreated = DateTime.UtcNow.AddDays(-3);
        var dao = new TestProductDao
        {
            CreatedOnUtc = originalCreated,
            CreatedBy = "original-creator"
        };

        mgr.SetUpsertAuditFields(dao);

        dao.CreatedOnUtc.Should().Be(originalCreated);
        dao.CreatedBy.Should().Be("original-creator");
        dao.UpdatedBy.Should().Be("eve");
        dao.UpdatedOnUtc.Should().NotBeNull();
    }

    [Fact]
    public void SetUpsertAuditFields_MinValue_CreatedOnUtc_Should_Treat_As_New()
    {
        var mgr = BuildManager("frank");
        var dao = new TestProductDao { CreatedOnUtc = DateTime.MinValue };

        mgr.SetUpsertAuditFields(dao);

        dao.CreatedBy.Should().Be("frank",
            "DateTime.MinValue is treated as 'not set', so the create branch should run");
        dao.Deleted.Should().BeFalse();
    }

    // ── SetBulkAuditFields ─────────────────────────────────────────

    [Fact]
    public void SetBulkAuditFields_Create_Should_Set_All_Fields_On_Every_Item()
    {
        var mgr = BuildManager("grace");
        var items = Enumerable.Range(0, 3).Select(_ => new TestProductDao()).ToList();

        mgr.SetBulkAuditFields(items, isCreateOperation: true);

        foreach (var dao in items)
        {
            dao.CreatedOnUtc.Should().NotBeNull();
            dao.UpdatedOnUtc.Should().NotBeNull();
            dao.CreatedBy.Should().Be("grace");
            dao.UpdatedBy.Should().Be("grace");
            dao.Deleted.Should().BeFalse();
        }
    }

    [Fact]
    public void SetBulkAuditFields_Upsert_New_Items_Should_Set_Created_Fields()
    {
        var mgr = BuildManager("heidi");
        var items = new[] { new TestProductDao { CreatedOnUtc = null } };

        mgr.SetBulkAuditFields(items, isCreateOperation: false);

        items[0].CreatedOnUtc.Should().NotBeNull();
        items[0].CreatedBy.Should().Be("heidi");
    }

    [Fact]
    public void SetBulkAuditFields_Upsert_Existing_Items_Should_Preserve_Created_Fields()
    {
        var mgr = BuildManager("ivan");
        var originalCreated = DateTime.UtcNow.AddDays(-7);
        var items = new[]
        {
            new TestProductDao { CreatedOnUtc = originalCreated, CreatedBy = "founder" }
        };

        mgr.SetBulkAuditFields(items, isCreateOperation: false);

        items[0].CreatedOnUtc.Should().Be(originalCreated);
        items[0].CreatedBy.Should().Be("founder");
        items[0].UpdatedBy.Should().Be("ivan");
    }

    // ── Constructor ────────────────────────────────────────────────

    [Fact]
    public void Constructor_Should_Throw_ArgumentNullException_For_Null_UserContext()
    {
        var act = () => new AuditFieldManager<TestProductDao>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetCreateAuditFields_Should_Use_Current_User_From_Context()
    {
        var ctx = new Mock<IUserContext>();
        ctx.Setup(c => c.GetCurrentUser()).Returns("mocked-user");
        var mgr = new AuditFieldManager<TestProductDao>(ctx.Object);

        mgr.SetCreateAuditFields(new TestProductDao());

        ctx.Verify(c => c.GetCurrentUser(), Times.Once);
    }
}
