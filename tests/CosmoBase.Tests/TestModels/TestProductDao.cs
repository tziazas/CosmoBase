using CosmoBase.Abstractions.Interfaces;
using System.ComponentModel.DataAnnotations;
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
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public List<string> Tags { get; set; } = new();

    public ProductMetadata? Metadata { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? DiscontinuedDate { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public ProductDimensions? Dimensions { get; set; }
    
    [JsonPropertyName("customerId")]
    [Required]
    public string CustomerId { get; set; }
}