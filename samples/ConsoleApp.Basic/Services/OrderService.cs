using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Abstractions.Enums;
using ConsoleApp.Basic.Models;
using Microsoft.Extensions.Logging;

namespace ConsoleApp.Basic.Services;

public class OrderService
{
    private readonly ICosmosDataReadService<Order, OrderDao> _readService;
    private readonly ICosmosDataWriteService<Order, OrderDao> _writeService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        ICosmosDataReadService<Order, OrderDao> readService,
        ICosmosDataWriteService<Order, OrderDao> writeService,
        ILogger<OrderService> logger)
    {
        _readService = readService;
        _writeService = writeService;
        _logger = logger;
    }

    public async Task DemonstrateOrderOperationsAsync()
    {
        Console.WriteLine("1️⃣ Creating orders for different customers...");

        // Create orders for customer 1
        var order1 = new Order
        {
            CustomerId = "customer-001",
            OrderNumber = "ORD-2025-001",
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            TotalAmount = 1349.98m,
            Items = new List<OrderItem>
            {
                new OrderItem
                {
                    ProductId = "laptop-001",
                    ProductName = "Gaming Laptop",
                    Quantity = 1,
                    UnitPrice = 1299.99m
                },
                new OrderItem
                {
                    ProductId = "mouse-001",
                    ProductName = "Wireless Mouse",
                    Quantity = 1,
                    UnitPrice = 49.99m
                }
            }
        };

        var order2 = new Order
        {
            CustomerId = "customer-002",
            OrderNumber = "ORD-2025-002",
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            TotalAmount = 179.98m,
            Items = new List<OrderItem>
            {
                new OrderItem
                {
                    ProductId = "keyboard-001",
                    ProductName = "Mechanical Keyboard",
                    Quantity = 1,
                    UnitPrice = 129.99m
                },
                new OrderItem
                {
                    ProductId = "mouse-001",
                    ProductName = "Wireless Mouse",
                    Quantity = 1,
                    UnitPrice = 49.99m
                }
            }
        };

        var createdOrder1 = await _writeService.CreateAsync(order1);
        var createdOrder2 = await _writeService.CreateAsync(order2);

        Console.WriteLine($"✅ Created order for customer-001: {createdOrder1.OrderNumber} - ${createdOrder1.TotalAmount}");
        Console.WriteLine($"   📅 Created: {createdOrder1.CreatedOnUtc} by {createdOrder1.CreatedBy}");
        Console.WriteLine($"   📦 Items: {createdOrder1.Items.Count} items");

        Console.WriteLine($"✅ Created order for customer-002: {createdOrder2.OrderNumber} - ${createdOrder2.TotalAmount}");
        Console.WriteLine($"   📦 Items: {createdOrder2.Items.Count} items");

        Console.WriteLine("\n2️⃣ Reading orders by customer...");

        // Get all orders for customer 1
        var customer1Orders = new List<Order>();
        await foreach (var order in _readService.GetAllAsync("customer-001"))
        {
            customer1Orders.Add(order);
        }

        Console.WriteLine($"📖 Found {customer1Orders.Count} orders for customer-001:");
        foreach (var order in customer1Orders)
        {
            Console.WriteLine($"   🛒 {order.OrderNumber} - {order.Status} - ${order.TotalAmount}");
        }

        Console.WriteLine("\n3️⃣ Updating order status...");

        // Update order status
        createdOrder1.Status = OrderStatus.Confirmed;
        var updatedOrder = await _writeService.ReplaceAsync(createdOrder1);

        Console.WriteLine($"🔄 Updated order {updatedOrder.OrderNumber} status to: {updatedOrder.Status}");
        Console.WriteLine($"   📅 Updated: {updatedOrder.UpdatedOnUtc} by {updatedOrder.UpdatedBy}");
        Console.WriteLine($"   📅 Originally created: {updatedOrder.CreatedOnUtc} by {updatedOrder.CreatedBy}");

        Console.WriteLine("\n4️⃣ Creating orders for multiple customers...");

        var customers = new[] { "customer-003", "customer-004", "customer-005" };
        var createdOrders = new List<Order>();

        foreach (var customerId in customers)
        {
            var order = new Order
            {
                CustomerId = customerId,
                OrderNumber = $"ORD-2025-{customerId.Split('-')[1]}",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Pending,
                TotalAmount = 99.99m,
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        ProductId = "sample-product",
                        ProductName = "Sample Product",
                        Quantity = 1,
                        UnitPrice = 99.99m
                    }
                }
            };

            var created = await _writeService.CreateAsync(order);
            createdOrders.Add(created);
            Console.WriteLine($"✅ Created order for {customerId}: {created.OrderNumber}");
        }

        Console.WriteLine("\n5️⃣ Getting order counts per customer...");

        foreach (var customerId in customers)
        {
            var count = await _readService.GetCountAsync(customerId);
            Console.WriteLine($"📊 Customer {customerId}: {count} orders");
        }

        Console.WriteLine("\n6️⃣ Demonstrating order pagination...");

        // Get paginated orders for customer-001
        var (firstPage, continuationToken, totalCount) = await _readService.GetPageWithTokenAndCountAsync(
            new CosmoBase.Abstractions.Filters.SqlSpecification<Order>("SELECT * FROM c WHERE c.CustomerId = 'customer-001' ORDER BY c.OrderDate DESC"),
            "customer-001",
            pageSize: 2);

        Console.WriteLine($"📄 First page for customer-001: {firstPage.Count} orders (Total: {totalCount})");
        foreach (var order in firstPage)
        {
            Console.WriteLine($"   🛒 {order.OrderNumber} - {order.Status} - ${order.TotalAmount} ({order.OrderDate:yyyy-MM-dd})");
        }

        Console.WriteLine("\n7️⃣ Demonstrating soft delete...");

        // Soft delete an order
        var orderToDelete = createdOrders.First();
        await _writeService.DeleteAsync(orderToDelete.Id, orderToDelete.CustomerId, DeleteOptions.SoftDelete);
        Console.WriteLine($"🗑️ Soft deleted order: {orderToDelete.OrderNumber}");

        // Try to retrieve (should return null for soft-deleted)
        var retrievedAfterDelete = await _readService.GetByIdAsync(orderToDelete.Id, orderToDelete.CustomerId, includeDeleted: false);
        Console.WriteLine($"📖 Retrieved after soft delete (includeDeleted=false): {(retrievedAfterDelete == null ? "null" : "found")}");

        // Try to retrieve with includeDeleted=true
        var retrievedIncludeDeleted = await _readService.GetByIdAsync(orderToDelete.Id, orderToDelete.CustomerId, includeDeleted: true);
        Console.WriteLine($"📖 Retrieved with includeDeleted=true: {(retrievedIncludeDeleted == null ? "null" : "found - deleted=" + retrievedIncludeDeleted.Deleted)}");
    }
}