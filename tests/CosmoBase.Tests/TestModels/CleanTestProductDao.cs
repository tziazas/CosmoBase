using System.Text.Json.Serialization;
using CosmoBase.Abstractions.Interfaces;

namespace CosmoBase.Tests.TestModels;
public class CleanTestProductDao : ICosmosDataModel
{
    public string id { get; set; } = string.Empty;
    public string Id { get; set; }
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class TestDaoWithJsonAttr : ICosmosDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; }
    
    public string Category { get; set; } = string.Empty;
    
    [JsonPropertyName("customerId")]  // This is likely the problem
    public string CustomerId { get; set; } = string.Empty;
}

public class TestProductDaoNoValidation : ICosmosDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    // ... other properties without validation attributes
}

public class SimpleTestProductDao : ICosmosDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime? DiscontinuedDate { get; set; }
    public int StockQuantity { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    // NO Tags, Metadata, or Dimensions
}

public class ExactCopyDao : ICosmosDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class SimpleTestClass
{
    public string id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class MinimalCosmosClass : ICosmosDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; }
    
    public string Category { get; set; } = string.Empty;
}