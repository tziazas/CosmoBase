using WebApi.Advanced.Models;

namespace WebApi.Advanced.Services;

/// <summary>
/// Service interface for product operations
/// </summary>
public interface IProductService
{
    Task<ProductResponseDto> CreateProductAsync(CreateProductRequestDto request, CancellationToken cancellationToken = default);
    Task<ProductResponseDto?> GetProductByIdAsync(string id, string category, CancellationToken cancellationToken = default);
    Task<ProductResponseDto> UpdateProductAsync(string id, UpdateProductRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(string id, string category, bool hardDelete = false, CancellationToken cancellationToken = default);
    Task<PagedResponseDto<ProductResponseDto>> GetProductsAsync(string category, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductResponseDto>> SearchProductsAsync(string searchTerm, string? category = null, CancellationToken cancellationToken = default);
    Task BulkImportProductsAsync(IEnumerable<CreateProductRequestDto> products, string category, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service interface for order operations
/// </summary>
public interface IOrderService
{
    Task<OrderResponseDto> CreateOrderAsync(CreateOrderRequestDto request, CancellationToken cancellationToken = default);
    Task<OrderResponseDto?> GetOrderByIdAsync(string id, string customerId, CancellationToken cancellationToken = default);
    Task<OrderResponseDto> UpdateOrderStatusAsync(string id, string customerId, OrderStatus newStatus, CancellationToken cancellationToken = default);
    Task<PagedResponseDto<OrderResponseDto>> GetOrdersForCustomerAsync(string customerId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<OrderSummaryDto> GetOrderSummaryAsync(string customerId, CancellationToken cancellationToken = default);
    Task CancelOrderAsync(string id, string customerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service interface for inventory management
/// </summary>
public interface IInventoryService
{
    Task<InventoryResponseDto?> GetInventoryAsync(string productId, string category, CancellationToken cancellationToken = default);
    Task UpdateStockAsync(string productId, string category, int newQuantity, CancellationToken cancellationToken = default);
    Task ReserveStockAsync(string productId, string category, int quantity, CancellationToken cancellationToken = default);
    Task ReleaseStockAsync(string productId, string category, int quantity, CancellationToken cancellationToken = default);
    Task<IEnumerable<LowStockAlertDto>> GetLowStockAlertsAsync(string category, int threshold = 10, CancellationToken cancellationToken = default);
}