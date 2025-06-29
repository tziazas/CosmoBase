using System.ComponentModel.DataAnnotations;

namespace WebApi.Advanced.Models;


#region Product DTOs

public class CreateProductRequestDto
{
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

    public List<string>? Tags { get; set; }

    [Range(0, int.MaxValue)]
    public int InitialStock { get; set; }

    [StringLength(50)]
    public string? Sku { get; set; }
}

public class UpdateProductRequestDto
{
    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal? Price { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public List<string>? Tags { get; set; }

    [Range(0, int.MaxValue)]
    public int? StockQuantity { get; set; }

    public bool? IsActive { get; set; }
}

public class ProductResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public int StockQuantity { get; set; }
    public string? Sku { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

#endregion

#region Order DTOs

public class CreateOrderRequestDto
{
    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    public List<OrderItemRequestDto> Items { get; set; } = new();

    public string? Notes { get; set; }
}

public class OrderItemRequestDto
{
    [Required]
    public string ProductId { get; set; } = string.Empty;

    [Required]
    public string ProductName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }
}

public class OrderResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderItemResponseDto> Items { get; set; } = new();
    public string? Notes { get; set; }
    public DateTime? CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public class OrderItemResponseDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class UpdateOrderStatusRequestDto
{
    [Required]
    public OrderStatus NewStatus { get; set; }
    
    public string? Notes { get; set; }
}

public class OrderSummaryDto
{
    public string CustomerId { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int CompletedOrders { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime? LastOrderDate { get; set; }
}

#endregion

#region Inventory DTOs

public class InventoryResponseDto
{
    public string ProductId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int AvailableStock { get; set; }
    public int ReservedStock { get; set; }
    public int TotalStock { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class UpdateStockRequestDto
{
    [Range(0, int.MaxValue)]
    public int NewQuantity { get; set; }
    
    public string? Reason { get; set; }
}

public class LowStockAlertDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int Threshold { get; set; }
    public string Sku { get; set; } = string.Empty;
}

#endregion

#region Common DTOs

public class PagedResponseDto<T>
{
    public IList<T> Items { get; set; } = new List<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage => Page > 1;
}

public class ApiResponseDto<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ErrorResponseDto
{
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? TraceId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

#endregion