using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CosmoBase.Abstractions.Interfaces;

namespace ConsoleApp.Basic.Models;

/// <summary>
/// Product DTO - exposed to application logic
/// </summary>
public class Product
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = new();

    public bool IsActive { get; set; } = true;

    public int StockQuantity { get; set; }

    public string? Sku { get; set; }

    // Audit fields (managed by CosmoBase)
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; }
}

/// <summary>
/// Product DAO - stored in Cosmos DB with audit fields
/// </summary>
public class ProductDao : ICosmosDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // ICosmosDataModel audit fields
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; }

    // Business properties
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = new();

    public bool IsActive { get; set; } = true;

    public int StockQuantity { get; set; }

    public string? Sku { get; set; }
}