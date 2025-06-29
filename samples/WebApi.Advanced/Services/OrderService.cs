using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Filters;
using WebApi.Advanced.Models;

namespace WebApi.Advanced.Services;

public class OrderService(
    ICosmosDataReadService<Order, OrderDao> readService,
    ICosmosDataWriteService<Order, OrderDao> writeService,
    ILogger<OrderService> logger)
    : IOrderService
{
    public async Task<OrderResponseDto> CreateOrderAsync(CreateOrderRequestDto request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating order for customer {CustomerId} with {ItemCount} items", 
            request.CustomerId, request.Items.Count);

        // Generate order number
        var orderNumber = GenerateOrderNumber();

        // Calculate total amount
        var totalAmount = request.Items.Sum(item => item.Quantity * item.UnitPrice);

        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = request.CustomerId,
            OrderNumber = orderNumber,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            TotalAmount = totalAmount,
            Items = request.Items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList(),
            Notes = request.Notes
        };

        var created = await writeService.CreateAsync(order, cancellationToken);
        
        logger.LogInformation("Successfully created order {OrderNumber} with ID {OrderId}", 
            created.OrderNumber, created.Id);
        
        return MapToResponseDto(created);
    }

    public async Task<OrderResponseDto?> GetOrderByIdAsync(string id, string customerId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving order {OrderId} for customer {CustomerId}", id, customerId);

        var order = await readService.GetByIdAsync(id, customerId, includeDeleted: false, cancellationToken);
        
        if (order == null)
        {
            logger.LogWarning("Order {OrderId} not found for customer {CustomerId}", id, customerId);
            return null;
        }

        return MapToResponseDto(order);
    }

    public async Task<OrderResponseDto> UpdateOrderStatusAsync(string id, string customerId, OrderStatus newStatus, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating order {OrderId} status to {NewStatus}", id, newStatus);

        var existing = await readService.GetByIdAsync(id, customerId, includeDeleted: false, cancellationToken);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Order {id} not found for customer {customerId}");
        }

        // Validate status transition
        ValidateStatusTransition(existing.Status, newStatus);

        existing.Status = newStatus;

        var updated = await writeService.ReplaceAsync(existing, cancellationToken);
        
        logger.LogInformation("Successfully updated order {OrderId} status to {NewStatus}", id, newStatus);
        
        return MapToResponseDto(updated);
    }

    public async Task<PagedResponseDto<OrderResponseDto>> GetOrdersForCustomerAsync(string customerId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving orders for customer {CustomerId}, page {Page}, size {PageSize}", 
            customerId, page, pageSize);

        var spec = new SqlSpecification<Order>(
            "SELECT * FROM c WHERE c.CustomerId = @customerId ORDER BY c.OrderDate DESC",
            new Dictionary<string, object> { ["@customerId"] = customerId });

        // For production, you'd want to implement proper continuation token handling
        string? continuationToken = null;

        var (items, nextToken, totalCount) = await readService.GetPageWithTokenAndCountAsync(
            spec, customerId, pageSize, continuationToken, cancellationToken);

        var orderDtos = items.Select(MapToResponseDto).ToList();

        return new PagedResponseDto<OrderResponseDto>
        {
            Items = orderDtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount ?? 0,
            HasNextPage = !string.IsNullOrEmpty(nextToken)
        };
    }

    public async Task<OrderSummaryDto> GetOrderSummaryAsync(string customerId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Generating order summary for customer {CustomerId}", customerId);

        var orders = new List<Order>();
        
        // Get all orders for the customer
        await foreach (var order in readService.GetAllAsync(customerId, cancellationToken))
        {
            orders.Add(order);
        }

        var summary = new OrderSummaryDto
        {
            CustomerId = customerId,
            TotalOrders = orders.Count,
            PendingOrders = orders.Count(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed),
            CompletedOrders = orders.Count(o => o.Status == OrderStatus.Delivered),
            TotalValue = orders.Sum(o => o.TotalAmount),
            LastOrderDate = orders.OrderByDescending(o => o.OrderDate).FirstOrDefault()?.OrderDate
        };

        logger.LogInformation("Generated summary for customer {CustomerId}: {TotalOrders} orders, ${TotalValue:F2} total value", 
            customerId, summary.TotalOrders, summary.TotalValue);

        return summary;
    }

    public async Task CancelOrderAsync(string id, string customerId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Cancelling order {OrderId} for customer {CustomerId}", id, customerId);

        var existing = await readService.GetByIdAsync(id, customerId, includeDeleted: false, cancellationToken);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Order {id} not found for customer {customerId}");
        }

        // Validate that order can be cancelled
        if (existing.Status == OrderStatus.Delivered || existing.Status == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot cancel order in {existing.Status} status");
        }

        // Soft delete the order instead of just changing status
        await writeService.DeleteAsync(id, customerId, DeleteOptions.SoftDelete, cancellationToken);

        logger.LogInformation("Successfully cancelled order {OrderId}", id);
    }

    private static void ValidateStatusTransition(OrderStatus currentStatus, OrderStatus newStatus)
    {
        // Define valid status transitions
        var validTransitions = new Dictionary<OrderStatus, List<OrderStatus>>
        {
            [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.Processing, OrderStatus.Cancelled],
            [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
            [OrderStatus.Shipped] = [OrderStatus.Delivered],
            [OrderStatus.Delivered] = [OrderStatus.Returned],
            [OrderStatus.Cancelled] = [], // No transitions from cancelled
            [OrderStatus.Returned] = [] // No transitions from returned
        };

        if (!validTransitions.ContainsKey(currentStatus))
        {
            throw new InvalidOperationException($"Unknown current status: {currentStatus}");
        }

        if (!validTransitions[currentStatus].Contains(newStatus))
        {
            throw new InvalidOperationException(
                $"Invalid status transition from {currentStatus} to {newStatus}. " +
                $"Valid transitions: {string.Join(", ", validTransitions[currentStatus])}");
        }
    }

    private static string GenerateOrderNumber()
    {
        // Generate order number with format: ORD-YYYY-XXXXXX
        var year = DateTime.UtcNow.Year;
        var randomPart = Random.Shared.Next(100000, 999999);
        return $"ORD-{year}-{randomPart:D6}";
    }

    private static OrderResponseDto MapToResponseDto(Order order)
    {
        return new OrderResponseDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            Items = order.Items.Select(item => new OrderItemResponseDto
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            }).ToList(),
            Notes = order.Notes,
            CreatedOnUtc = order.CreatedOnUtc,
            UpdatedOnUtc = order.UpdatedOnUtc,
            CreatedBy = order.CreatedBy,
            UpdatedBy = order.UpdatedBy
        };
    }
}