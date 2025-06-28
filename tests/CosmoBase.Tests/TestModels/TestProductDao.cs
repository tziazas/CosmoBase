using CosmoBase.Abstractions.Interfaces;
using System.Text.Json.Serialization;

namespace CosmoBase.Tests.TestModels;

/// <summary>
/// Data Access Object for TestProduct - includes audit fields for Cosmos DB persistence
/// </summary>
public class TestProductDao : ICosmosDataModel
{
    // ICosmosDataModel implementation
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; } = false;

    // Business properties (same as DTO)

    public string Name { get; set; } = string.Empty;

    public string CustomerId { get; set; }

    public string Category { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = new();

    public ProductMetadata? Metadata { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? DiscontinuedDate { get; set; }

    public int StockQuantity { get; set; }

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public ProductDimensions? Dimensions { get; set; }
}