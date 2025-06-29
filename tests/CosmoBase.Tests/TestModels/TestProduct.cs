using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CosmoBase.Abstractions.Interfaces;

namespace CosmoBase.Tests.TestModels;

/// <summary>
/// Test model representing a product for testing CosmoBase functionality
/// </summary>
public class TestProduct
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

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

    // Audit fields (should be managed by CosmoBase)
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; } = false;
}

public class ProductMetadata
{
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public double? Weight { get; set; }
    public string? Material { get; set; }
    public string? Origin { get; set; }
    public Dictionary<string, object> CustomAttributes { get; set; } = new();
}

public class ProductDimensions
{
    public double Length { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Unit { get; set; } = "cm";
}