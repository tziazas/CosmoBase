using CosmoBase.Abstractions.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CosmoBase.Tests.TestModels;

/// <summary>
/// Data Access Object for TestOrder - includes audit fields for Cosmos DB persistence
/// </summary>
public class TestOrderDao : ICosmosDataModel
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
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    public string OrderNumber { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [Range(0.01, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    public decimal ShippingCost { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public List<OrderItem> Items { get; set; } = new();

    public ShippingAddress? ShippingAddress { get; set; }

    public BillingAddress? BillingAddress { get; set; }

    public PaymentInfo? PaymentInfo { get; set; }

    public string? Notes { get; set; }

    public DateTime? ShippedDate { get; set; }

    public DateTime? DeliveredDate { get; set; }

    public string? TrackingNumber { get; set; }
}