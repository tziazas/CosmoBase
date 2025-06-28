using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CosmoBase.Abstractions.Interfaces;

namespace ConsoleApp.Basic.Models;

/// <summary>
/// Order DTO - exposed to application logic
/// </summary>
public class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    public string OrderNumber { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [Range(0.01, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    public List<OrderItem> Items { get; set; } = new();

    // Audit fields (managed by CosmoBase)
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; }
}

/// <summary>
/// Order DAO - stored in Cosmos DB with audit fields
/// </summary>
public class OrderDao : ICosmosDataModel
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
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    public string OrderNumber { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [Range(0.01, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    [Required]
    public string ProductId { get; set; } = string.Empty;

    [Required]
    public string ProductName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    public decimal TotalPrice => Quantity * UnitPrice;
}

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Processing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5
}