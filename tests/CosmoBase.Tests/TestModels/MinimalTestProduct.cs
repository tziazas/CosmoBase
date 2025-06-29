using System.Text.Json.Serialization;
using CosmoBase.Abstractions.Interfaces;

namespace CosmoBase.Tests.TestModels;

/// <summary>
/// Ultra-minimal product for debugging UTF-8 issues
/// </summary>
public class MinimalTestProduct
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; }
}

/// <summary>
/// Ultra-minimal DAO for debugging UTF-8 issues
/// </summary>
public class MinimalTestProductDao : ICosmosDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
        
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
        
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; }
}