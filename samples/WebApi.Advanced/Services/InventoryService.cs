using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Abstractions.Filters;
using WebApi.Advanced.Models;
using Microsoft.Extensions.Logging;

namespace WebApi.Advanced.Services;

public class InventoryService : IInventoryService
{
    private readonly ICosmosDataReadService<Product, ProductDao> _productReadService;
    private readonly ICosmosDataWriteService<Product, ProductDao> _productWriteService;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        ICosmosDataReadService<Product, ProductDao> productReadService,
        ICosmosDataWriteService<Product, ProductDao> productWriteService,
        ILogger<InventoryService> logger)
    {
        _productReadService = productReadService;
        _productWriteService = productWriteService;
        _logger = logger;
    }

    public async Task<InventoryResponseDto?> GetInventoryAsync(string productId, string category, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving inventory for product {ProductId} in category {Category}", productId, category);

        var product = await _productReadService.GetByIdAsync(productId, category, includeDeleted: false, cancellationToken);
        
        if (product == null)
        {
            _logger.LogWarning("Product {ProductId} not found in category {Category}", productId, category);
            return null;
        }

        // For this demo, we're using the product's StockQuantity as available stock
        // In a real system, you might have separate inventory tracking
        return new InventoryResponseDto
        {
            ProductId = productId,
            Category = category,
            AvailableStock = product.StockQuantity,
            ReservedStock = 0, // Would come from a separate reservation system
            TotalStock = product.StockQuantity,
            LastUpdated = product.UpdatedOnUtc ?? product.CreatedOnUtc ?? DateTime.UtcNow
        };
    }

    public async Task UpdateStockAsync(string productId, string category, int newQuantity, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating stock for product {ProductId} to {NewQuantity}", productId, newQuantity);

        if (newQuantity < 0)
        {
            throw new ArgumentException("Stock quantity cannot be negative", nameof(newQuantity));
        }

        var product = await _productReadService.GetByIdAsync(productId, category, includeDeleted: false, cancellationToken);
        if (product == null)
        {
            throw new KeyNotFoundException($"Product {productId} not found in category {category}");
        }

        var oldQuantity = product.StockQuantity;
        product.StockQuantity = newQuantity;

        await _productWriteService.ReplaceAsync(product, cancellationToken);

        _logger.LogInformation("Successfully updated stock for product {ProductId} from {OldQuantity} to {NewQuantity}", 
            productId, oldQuantity, newQuantity);
    }

    public async Task ReserveStockAsync(string productId, string category, int quantity, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reserving {Quantity} units of product {ProductId}", quantity, productId);

        if (quantity <= 0)
        {
            throw new ArgumentException("Reservation quantity must be positive", nameof(quantity));
        }

        var product = await _productReadService.GetByIdAsync(productId, category, includeDeleted: false, cancellationToken);
        if (product == null)
        {
            throw new KeyNotFoundException($"Product {productId} not found in category {category}");
        }

        if (product.StockQuantity < quantity)
        {
            throw new InvalidOperationException(
                $"Insufficient stock for product {productId}. Available: {product.StockQuantity}, Requested: {quantity}");
        }

        // Reduce available stock (in a real system, you'd move this to reserved stock)
        product.StockQuantity -= quantity;

        await _productWriteService.ReplaceAsync(product, cancellationToken);

        _logger.LogInformation("Successfully reserved {Quantity} units of product {ProductId}. Remaining stock: {RemainingStock}", 
            quantity, productId, product.StockQuantity);
    }

    public async Task ReleaseStockAsync(string productId, string category, int quantity, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Releasing {Quantity} units of product {ProductId}", quantity, productId);

        if (quantity <= 0)
        {
            throw new ArgumentException("Release quantity must be positive", nameof(quantity));
        }

        var product = await _productReadService.GetByIdAsync(productId, category, includeDeleted: false, cancellationToken);
        if (product == null)
        {
            throw new KeyNotFoundException($"Product {productId} not found in category {category}");
        }

        // Return stock (in a real system, you'd move this from reserved back to available)
        product.StockQuantity += quantity;

        await _productWriteService.ReplaceAsync(product, cancellationToken);

        _logger.LogInformation("Successfully released {Quantity} units of product {ProductId}. New stock: {NewStock}", 
            quantity, productId, product.StockQuantity);
    }

    public async Task<IEnumerable<LowStockAlertDto>> GetLowStockAlertsAsync(string category, int threshold = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving low stock alerts for category {Category} with threshold {Threshold}", category, threshold);

        var spec = new SqlSpecification<Product>(
            "SELECT * FROM c WHERE c.Category = @category AND c.StockQuantity <= @threshold AND c.IsActive = true ORDER BY c.StockQuantity ASC",
            new Dictionary<string, object> 
            { 
                ["@category"] = category,
                ["@threshold"] = threshold
            });

        var lowStockProducts = new List<Product>();
        
        await foreach (var product in _productReadService.QueryAsync(spec, cancellationToken))
        {
            lowStockProducts.Add(product);
        }

        var alerts = lowStockProducts.Select(product => new LowStockAlertDto
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Category = product.Category,
            CurrentStock = product.StockQuantity,
            Threshold = threshold,
            Sku = product.Sku ?? "N/A"
        }).ToList();

        _logger.LogInformation("Found {AlertCount} low stock alerts for category {Category}", alerts.Count, category);

        return alerts;
    }
}