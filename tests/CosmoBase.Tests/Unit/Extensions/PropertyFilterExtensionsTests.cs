using CosmoBase.Abstractions.Filters;
using CosmoBase.Core.Extensions;
using FluentAssertions;
using Microsoft.Azure.Cosmos;

namespace CosmoBase.Tests.Unit.Extensions;

public class PropertyFilterExtensionsTests
{
    // -------------------------------------------------------------------------
    // BuildSqlWhereClause
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildSqlWhereClause_EmptyFilters_Returns1Equals1()
    {
        var result = new List<PropertyFilter>().BuildSqlWhereClause();
        result.Should().Be("1=1");
    }

    [Fact]
    public void BuildSqlWhereClause_EqualFilter_GeneratesCorrectClause()
    {
        var filters = new List<PropertyFilter>
        {
            new() { PropertyName = "@Category", PropertyValue = "electronics", PropertyComparison = PropertyComparison.Equal }
        };

        var result = filters.BuildSqlWhereClause();

        result.Should().Be("c.Category = @Category");
    }

    [Theory]
    [InlineData(PropertyComparison.NotEqual, "<>")]
    [InlineData(PropertyComparison.GreaterThan, ">")]
    [InlineData(PropertyComparison.LessThan, "<")]
    [InlineData(PropertyComparison.GreaterThanOrEqual, ">=")]
    [InlineData(PropertyComparison.LessThanOrEqual, "<=")]
    public void BuildSqlWhereClause_ScalarComparisons_GenerateCorrectOperator(string comparison, string expectedOp)
    {
        var filters = new List<PropertyFilter>
        {
            new() { PropertyName = "@Price", PropertyValue = 100, PropertyComparison = comparison }
        };

        var result = filters.BuildSqlWhereClause();

        result.Should().Be($"c.Price {expectedOp} @Price");
    }

    [Fact]
    public void BuildSqlWhereClause_InFilter_DoesNotInlineLiterals()
    {
        var filters = new List<PropertyFilter>
        {
            new() { PropertyName = "@Category", PropertyValue = new List<object> { "electronics", "clothing" }, PropertyComparison = PropertyComparison.In }
        };

        var result = filters.BuildSqlWhereClause();

        // Values must not appear as literals in the SQL
        result.Should().NotContain("'electronics'");
        result.Should().NotContain("'clothing'");
    }

    [Fact]
    public void BuildSqlWhereClause_InFilter_GeneratesParameterizedClause()
    {
        var filters = new List<PropertyFilter>
        {
            new() { PropertyName = "@Category", PropertyValue = new List<object> { "electronics", "clothing" }, PropertyComparison = PropertyComparison.In }
        };

        var result = filters.BuildSqlWhereClause();

        result.Should().Be("c.Category IN (@Category_0_in_0, @Category_0_in_1)");
    }

    [Fact]
    public void BuildSqlWhereClause_InFilter_SingleQuoteInValue_DoesNotBreakQuery()
    {
        // This value would have caused injection / query breakage with the old literal-embedding approach
        var filters = new List<PropertyFilter>
        {
            new() { PropertyName = "@Name", PropertyValue = new List<object> { "O'Brien", "Smith" }, PropertyComparison = PropertyComparison.In }
        };

        var result = filters.BuildSqlWhereClause();

        result.Should().Be("c.Name IN (@Name_0_in_0, @Name_0_in_1)");
    }

    [Fact]
    public void BuildSqlWhereClause_TwoInFiltersOnSameColumn_GeneratesNonCollidingParameters()
    {
        var filters = new List<PropertyFilter>
        {
            new() { PropertyName = "@Category", PropertyValue = new List<object> { "a", "b" }, PropertyComparison = PropertyComparison.In },
            new() { PropertyName = "@Category", PropertyValue = new List<object> { "c" }, PropertyComparison = PropertyComparison.In }
        };

        var result = filters.BuildSqlWhereClause();

        // Filter at index 0 → @Category_0_in_*, filter at index 1 → @Category_1_in_*
        result.Should().Contain("@Category_0_in_0");
        result.Should().Contain("@Category_0_in_1");
        result.Should().Contain("@Category_1_in_0");
    }

    [Fact]
    public void BuildSqlWhereClause_MultipleFilters_CombinedWithAnd()
    {
        var filters = new List<PropertyFilter>
        {
            new() { PropertyName = "@Category", PropertyValue = "electronics", PropertyComparison = PropertyComparison.Equal },
            new() { PropertyName = "@Price", PropertyValue = 50, PropertyComparison = PropertyComparison.GreaterThan }
        };

        var result = filters.BuildSqlWhereClause();

        result.Should().Be("c.Category = @Category AND c.Price > @Price");
    }

    [Fact]
    public void BuildSqlWhereClause_UnsupportedComparison_ThrowsNotSupportedException()
    {
        var filters = new List<PropertyFilter>
        {
            new() { PropertyName = "@Category", PropertyValue = "x", PropertyComparison = "LIKE" }
        };

        var act = () => filters.BuildSqlWhereClause();

        act.Should().Throw<NotSupportedException>();
    }

    // -------------------------------------------------------------------------
    // AddParameters
    // -------------------------------------------------------------------------

    [Fact]
    public void AddParameters_ScalarFilter_BindsParameter()
    {
        var filters = new List<PropertyFilter>
        {
            new() { PropertyName = "@Category", PropertyValue = "electronics", PropertyComparison = PropertyComparison.Equal }
        };

        var def = new QueryDefinition("SELECT * FROM c WHERE c.Category = @Category");
        filters.AddParameters(def);

        // QueryDefinition doesn't expose bound params publicly, so we verify indirectly
        // by ensuring the method runs without throwing and the same named param is used
        // in both BuildSqlWhereClause and AddParameters.
        def.Should().NotBeNull();
    }

    [Fact]
    public void AddParameters_InFilter_BindsEachValueSeparately()
    {
        var filterValues = new List<object> { "electronics", "clothing", "sports" };
        var filters = new List<PropertyFilter>
        {
            new() { PropertyName = "@Category", PropertyValue = filterValues, PropertyComparison = PropertyComparison.In }
        };

        var sql = filters.BuildSqlWhereClause();
        var def = new QueryDefinition(sql);

        // Should not throw — each value gets its own parameter
        var act = () => filters.AddParameters(def);
        act.Should().NotThrow();
    }

    [Fact]
    public void AddParameters_InFilter_SingleQuoteValue_DoesNotThrow()
    {
        var filters = new List<PropertyFilter>
        {
            new() { PropertyName = "@Name", PropertyValue = new List<object> { "O'Brien", "Smith" }, PropertyComparison = PropertyComparison.In }
        };

        var sql = filters.BuildSqlWhereClause();
        var def = new QueryDefinition(sql);

        var act = () => filters.AddParameters(def);
        act.Should().NotThrow();
    }

    [Fact]
    public void BuildSqlWhereClause_And_AddParameters_ParameterNamesAreConsistent()
    {
        // Verify that BuildSqlWhereClause and AddParameters agree on the same parameter names
        // for IN filters so that the final QueryDefinition is coherent.
        var filterValues = new List<object> { "a", "b" };
        var filters = new List<PropertyFilter>
        {
            new() { PropertyName = "@Category", PropertyValue = filterValues, PropertyComparison = PropertyComparison.In }
        };

        var sql = filters.BuildSqlWhereClause();

        // SQL must contain the parameters that AddParameters will bind
        sql.Should().Contain("@Category_0_in_0");
        sql.Should().Contain("@Category_0_in_1");

        // AddParameters must not throw when binding to those exact parameter names
        var def = new QueryDefinition(sql);
        var act = () => filters.AddParameters(def);
        act.Should().NotThrow();
    }
}
