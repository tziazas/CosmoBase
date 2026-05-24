using CosmoBase.Core.Validation;
using CosmoBase.Tests.TestModels;
using FluentAssertions;

namespace CosmoBase.Tests.Unit.Validators;

/// <summary>
/// Unit tests for the array-property-query validation added to fix SQL injection
/// via interpolated identifier names in GetAllByArrayPropertyAsync.
/// </summary>
public class CosmosValidatorArrayQueryTests
{
    private readonly CosmosValidator<SimpleTestProductDao> _validator = new();

    // -------------------------------------------------------------------------
    // Valid names — should not throw
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Items")]
    [InlineData("orderItems")]
    [InlineData("order_items")]
    [InlineData("_privateArray")]
    [InlineData("metadata.tags")]          // nested path
    [InlineData("order.line.items")]       // deeply nested path
    [InlineData("Items123")]
    public void ValidateArrayPropertyQuery_ValidArrayName_DoesNotThrow(string arrayName)
    {
        var act = () => _validator.ValidateArrayPropertyQuery(arrayName, "id", "someValue");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("id")]
    [InlineData("orderId")]
    [InlineData("product_id")]
    [InlineData("_ref")]
    [InlineData("nested.prop")]
    public void ValidateArrayPropertyQuery_ValidElementPropertyName_DoesNotThrow(string elementPropertyName)
    {
        var act = () => _validator.ValidateArrayPropertyQuery("Items", elementPropertyName, "someValue");
        act.Should().NotThrow();
    }

    // -------------------------------------------------------------------------
    // Invalid array names — injection attempts
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Items; DROP TABLE c")]
    [InlineData("Items' OR '1'='1")]
    [InlineData("Items OR 1=1")]
    [InlineData("c.Items) UNION SELECT")]
    [InlineData("Items/*comment*/")]
    [InlineData("Items\nAND 1=1")]
    [InlineData("123Items")]               // starts with digit
    [InlineData(" Items")]                 // leading space
    [InlineData("Items ")]                 // trailing space
    [InlineData("Items-Name")]             // hyphen
    [InlineData("Items[0]")]               // array indexer
    public void ValidateArrayPropertyQuery_InvalidArrayName_ThrowsArgumentException(string arrayName)
    {
        var act = () => _validator.ValidateArrayPropertyQuery(arrayName, "id", "someValue");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("arrayName");
    }

    // -------------------------------------------------------------------------
    // Invalid element property names — injection attempts
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("id' OR '1'='1")]
    [InlineData("id; DROP")]
    [InlineData("id OR 1=1")]
    [InlineData("') UNION SELECT")]
    [InlineData("id\t")]                   // tab character
    [InlineData("123id")]                  // starts with digit
    [InlineData("id-ref")]                 // hyphen
    public void ValidateArrayPropertyQuery_InvalidElementPropertyName_ThrowsArgumentException(string elementPropertyName)
    {
        var act = () => _validator.ValidateArrayPropertyQuery("Items", elementPropertyName, "someValue");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("elementPropertyName");
    }

    // -------------------------------------------------------------------------
    // Existing null / empty guards still hold
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidateArrayPropertyQuery_NullArrayName_ThrowsArgumentException()
    {
        var act = () => _validator.ValidateArrayPropertyQuery(null!, "id", "value");
        act.Should().Throw<ArgumentException>().WithParameterName("arrayName");
    }

    [Fact]
    public void ValidateArrayPropertyQuery_EmptyArrayName_ThrowsArgumentException()
    {
        var act = () => _validator.ValidateArrayPropertyQuery("", "id", "value");
        act.Should().Throw<ArgumentException>().WithParameterName("arrayName");
    }

    [Fact]
    public void ValidateArrayPropertyQuery_NullElementPropertyName_ThrowsArgumentException()
    {
        var act = () => _validator.ValidateArrayPropertyQuery("Items", null!, "value");
        act.Should().Throw<ArgumentException>().WithParameterName("elementPropertyName");
    }

    [Fact]
    public void ValidateArrayPropertyQuery_EmptyElementPropertyName_ThrowsArgumentException()
    {
        var act = () => _validator.ValidateArrayPropertyQuery("Items", "", "value");
        act.Should().Throw<ArgumentException>().WithParameterName("elementPropertyName");
    }

    [Fact]
    public void ValidateArrayPropertyQuery_NullValue_ThrowsArgumentNullException()
    {
        var act = () => _validator.ValidateArrayPropertyQuery("Items", "id", null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("elementPropertyValue");
    }
}
