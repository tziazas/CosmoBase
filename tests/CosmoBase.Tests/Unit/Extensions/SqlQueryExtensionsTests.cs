using CosmoBase.Abstractions.Filters;
using CosmoBase.Core.Extensions;
using FluentAssertions;

namespace CosmoBase.Tests.Unit.Extensions;

public class SqlQueryExtensionsTests
{
    // ------------------------------------------------------------------
    // Helper: call ConvertToCountQuery and return the generated SQL text.
    // QueryDefinition does not expose its text directly, so we round-trip
    // through a known parameter to verify the query was built correctly.
    // ------------------------------------------------------------------
    private static string CountSql(string queryText, Dictionary<string, object>? parameters = null)
    {
        var spec = new SqlSpecification<object>(queryText, parameters);
        return GetQueryText(spec.ConvertToCountQuery());
    }

    private static string GetQueryText(Microsoft.Azure.Cosmos.QueryDefinition def)
        => def.QueryText;

    // ------------------------------------------------------------------ SELECT clause rewriting

    [Fact]
    public void ConvertToCountQuery_StarProjection_RewritesSelectClause()
    {
        var result = CountSql("SELECT * FROM c WHERE c.Deleted = false");
        result.Should().StartWith("SELECT VALUE COUNT(1) FROM c");
    }

    [Fact]
    public void ConvertToCountQuery_SingleNamedField_RewritesSelectClause()
    {
        var result = CountSql("SELECT c.Name FROM c WHERE c.Category = @cat",
            new() { ["@cat"] = "electronics" });
        result.Should().StartWith("SELECT VALUE COUNT(1) FROM c");
        result.Should().Contain("@cat");
    }

    [Fact]
    public void ConvertToCountQuery_MultipleNamedFields_RewritesSelectClause()
    {
        var result = CountSql("SELECT c.Id, c.Name, c.Price FROM c WHERE c.Deleted = false");
        result.Should().StartWith("SELECT VALUE COUNT(1) FROM c");
    }

    [Fact]
    public void ConvertToCountQuery_SelectValue_RewritesSelectClause()
    {
        var result = CountSql("SELECT VALUE c.Category FROM c");
        result.Should().StartWith("SELECT VALUE COUNT(1) FROM c");
    }

    [Fact]
    public void ConvertToCountQuery_CaseInsensitiveSelect_RewritesSelectClause()
    {
        var result = CountSql("select * from c where c.Deleted = false");
        result.Should().StartWith("SELECT VALUE COUNT(1) FROM c");
    }

    [Fact]
    public void ConvertToCountQuery_MixedCaseKeywords_RewritesSelectClause()
    {
        var result = CountSql("Select * From c Where c.Deleted = false");
        result.Should().StartWith("SELECT VALUE COUNT(1) FROM c");
    }

    // ------------------------------------------------------------------ ORDER BY removal

    [Fact]
    public void ConvertToCountQuery_WithOrderBy_RemovesOrderByClause()
    {
        var result = CountSql("SELECT * FROM c WHERE c.Category = @cat ORDER BY c.Name ASC",
            new() { ["@cat"] = "books" });
        result.Should().NotContain("ORDER BY", because: "ORDER BY is invalid in a COUNT query");
        result.Should().StartWith("SELECT VALUE COUNT(1) FROM c");
    }

    [Fact]
    public void ConvertToCountQuery_WithOrderByLowercase_RemovesOrderByClause()
    {
        var result = CountSql("SELECT * FROM c order by c.CreatedOnUtc DESC");
        result.Should().NotContainAny("order by", "ORDER BY");
    }

    [Fact]
    public void ConvertToCountQuery_WithMultiFieldOrderBy_RemovesEntireClause()
    {
        var result = CountSql("SELECT * FROM c WHERE c.Deleted = false ORDER BY c.Name ASC, c.Price DESC");
        result.Should().NotContain("ORDER BY");
        result.Should().Contain("WHERE c.Deleted = false");
    }

    // ------------------------------------------------------------------ OFFSET / LIMIT removal

    [Fact]
    public void ConvertToCountQuery_WithOffsetLimit_RemovesPaginationClause()
    {
        var result = CountSql("SELECT * FROM c WHERE c.Deleted = false OFFSET 0 LIMIT 20");
        result.Should().NotContain("OFFSET", because: "pagination is irrelevant for a count");
        result.Should().NotContain("LIMIT");
        result.Should().StartWith("SELECT VALUE COUNT(1) FROM c");
    }

    [Fact]
    public void ConvertToCountQuery_WithOffsetLimitParameters_RemovesPaginationClause()
    {
        var result = CountSql(
            "SELECT * FROM c WHERE c.Category = @cat OFFSET @offset LIMIT @limit",
            new() { ["@cat"] = "home", ["@offset"] = 0, ["@limit"] = 10 });
        result.Should().NotContain("OFFSET");
        result.Should().NotContain("LIMIT");
        result.Should().Contain("@cat");
    }

    [Fact]
    public void ConvertToCountQuery_WithOrderByAndOffsetLimit_RemovesBoth()
    {
        var result = CountSql(
            "SELECT * FROM c WHERE c.Deleted = false ORDER BY c.Name ASC OFFSET @offset LIMIT @limit",
            new() { ["@offset"] = 0, ["@limit"] = 50 });
        result.Should().NotContain("ORDER BY");
        result.Should().NotContain("OFFSET");
        result.Should().NotContain("LIMIT");
        result.Should().Contain("WHERE c.Deleted = false");
    }

    // ------------------------------------------------------------------ JOIN queries

    [Fact]
    public void ConvertToCountQuery_WithJoin_RewritesSelectClause()
    {
        var result = CountSql("SELECT * FROM c JOIN i IN c.Items WHERE i.Status = @status",
            new() { ["@status"] = "active" });
        result.Should().StartWith("SELECT VALUE COUNT(1) FROM c");
        result.Should().Contain("JOIN i IN c.Items");
        result.Should().Contain("@status");
    }

    // ------------------------------------------------------------------ Parameter preservation

    [Fact]
    public void ConvertToCountQuery_PreservesAllParameters()
    {
        var result = CountSql(
            "SELECT * FROM c WHERE c.Category = @cat AND c.Price > @minPrice",
            new() { ["@cat"] = "electronics", ["@minPrice"] = 100 });
        result.Should().Contain("@cat");
        result.Should().Contain("@minPrice");
    }

    [Fact]
    public void ConvertToCountQuery_NoParameters_DoesNotThrow()
    {
        var act = () => CountSql("SELECT * FROM c WHERE c.Deleted = false");
        act.Should().NotThrow();
    }

    // ------------------------------------------------------------------ WHERE clause preservation

    [Fact]
    public void ConvertToCountQuery_PreservesWhereClause()
    {
        var result = CountSql(
            "SELECT * FROM c WHERE c.Category = @cat AND c.Deleted = false",
            new() { ["@cat"] = "sports" });
        result.Should().Contain("WHERE c.Category = @cat AND c.Deleted = false");
    }

    // ------------------------------------------------------------------ Unsupported spec type

    [Fact]
    public void ConvertToCountQuery_NonSqlSpecification_ThrowsNotSupportedException()
    {
        var spec = new UnsupportedSpec();
        var act = () => spec.ConvertToCountQuery();
        act.Should().Throw<NotSupportedException>();
    }

    private class UnsupportedSpec : ISpecification<object> { }
}
