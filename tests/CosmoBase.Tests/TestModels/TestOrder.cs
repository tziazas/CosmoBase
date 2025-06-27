using System.ComponentModel.DataAnnotations;
using CosmoBase.Abstractions.Interfaces;

namespace CosmoBase.Tests.TestModels;

/// <summary>
/// Test model representing an order for testing CosmoBase functionality
/// </summary>
public class TestOrder
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

    // Audit fields (should be managed by CosmoBase)
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool Deleted { get; set; } = false;
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