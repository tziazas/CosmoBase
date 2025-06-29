using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CosmoBase.Abstractions.Interfaces;

namespace WebApi.Advanced.Models;

#region Product Models

/// <summary>
/// Product DTO - exposed to application logic and API
/// </summary>
public class Product
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public List<string> Tags { get; set; } = new();

    public bool IsActive { get; set; } = true;

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    [StringLength(50)]
    public string? Sku { get; set; }

    public ProductMetadata? Metadata { get; set; }

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
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public List<string> Tags { get; set; } = new();

    public bool IsActive { get; set; } = true;

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    [StringLength(50)]
    public string? Sku { get; set; }

    public ProductMetadata? Metadata { get; set; }
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

#endregion

#region Order Models

/// <summary>
/// Order DTO - exposed to application logic and API
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

public class OrderItem
{
    [Required]
    public string ProductId { get; set; } = string.Empty;

    [Required]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    public string Sku { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    public decimal TotalPrice => Quantity * UnitPrice;

    public Dictionary<string, object> Attributes { get; set; } = new();
}

public class ShippingAddress
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    public string State { get; set; } = string.Empty;

    [Required]
    public string PostalCode { get; set; } = string.Empty;

    [Required]
    public string Country { get; set; } = string.Empty;

    public string? Phone { get; set; }
}

public class BillingAddress : ShippingAddress
{
    public string? Company { get; set; }
}

public class PaymentInfo
{
    public PaymentMethod Method { get; set; }
    public string? TransactionId { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
}

#endregion

#region Enums

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Processing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
    Returned = 6,
    Refunded = 7
}

public enum PaymentMethod
{
    CreditCard = 0,
    DebitCard = 1,
    PayPal = 2,
    BankTransfer = 3,
    CashOnDelivery = 4,
    Cryptocurrency = 5
}

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Captured = 2,
    Failed = 3,
    Cancelled = 4,
    Refunded = 5
}

#endregion