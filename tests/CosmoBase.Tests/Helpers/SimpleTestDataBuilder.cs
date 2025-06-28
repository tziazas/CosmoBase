using CosmoBase.Tests.TestModels;

namespace CosmoBase.Tests.Helpers;

/// <summary>
/// Simple test data builder that generates basic, UTF-8 safe test data
/// Use this if the Bogus approach still causes issues
/// </summary>
public static class SimpleTestDataBuilder
{
    private static int _counter;
    
    public static TestProduct CreateSimpleTestProduct(string? category = null, string? id = null)
    {
        var counter = Interlocked.Increment(ref _counter);
        
        return new TestProduct
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Name = $"Test Product {counter}",
            Category = category ?? "electronics",
            CustomerId = Guid.NewGuid().ToString(),
            Price = 99.99m,
            Description = $"Test description for product {counter}",
            Tags = ["test", "sample", category ?? "electronics"],
            IsActive = true,
            StockQuantity = 100,
            Sku = $"SKU{counter:D6}",
            Barcode = $"BAR{counter:D8}",
            Metadata = new ProductMetadata
            {
                Brand = $"Test Brand {counter}",
                Model = $"Model-{counter}",
                Color = "Blue",
                Size = "M",
                Weight = 1.5,
                Material = "Cotton",
                Origin = "USA",
                CustomAttributes = new Dictionary<string, object>
                {
                    ["warranty"] = "12 months",
                    ["rating"] = 4.5,
                    ["reviews"] = 100
                }
            },
            Dimensions = new ProductDimensions
            {
                Length = 10.0,
                Width = 8.0,
                Height = 2.0,
                Unit = "cm"
            }
        };
    }

    public static TestOrder CreateSimpleTestOrder(string? customerId = null, string? id = null)
    {
        var counter = Interlocked.Increment(ref _counter);
        
        return new TestOrder
        {
            Id = id ?? Guid.NewGuid().ToString(),
            CustomerId = customerId ?? Guid.NewGuid().ToString(),
            OrderNumber = $"ORD{counter:D6}",
            OrderDate = DateTime.UtcNow.AddDays(-counter % 30),
            Status = OrderStatus.Pending,
            TotalAmount = 199.99m,
            ShippingCost = 9.99m,
            TaxAmount = 20.00m,
            DiscountAmount = 0m,
            Items =
            [
                new OrderItem
                {
                    ProductId = Guid.NewGuid().ToString(),
                    ProductName = $"Order Item {counter}",
                    Sku = $"SKU{counter:D6}",
                    Quantity = 2,
                    UnitPrice = 95.00m,
                    Attributes = new Dictionary<string, object>
                    {
                        ["color"] = "Red",
                        ["size"] = "L"
                    }
                }
            ],
            ShippingAddress = new ShippingAddress
            {
                Name = $"Test User {counter}",
                AddressLine1 = $"{counter} Test Street",
                City = "Test City",
                State = "Test State",
                PostalCode = "12345",
                Country = "USA",
                Phone = "555-123-4567"
            },
            PaymentInfo = new PaymentInfo
            {
                Method = PaymentMethod.CreditCard,
                TransactionId = $"TXN{counter:D8}",
                PaymentReference = $"REF{counter:D8}",
                ProcessedDate = DateTime.UtcNow,
                Status = PaymentStatus.Captured
            }
        };
    }

    public static List<TestProduct> CreateSimpleTestProducts(int count, string? category = null)
    {
        return Enumerable.Range(0, count)
            .Select(_ => CreateSimpleTestProduct(category))
            .ToList();
    }

    public static List<TestOrder> CreateSimpleTestOrders(int count, string? customerId = null)
    {
        return Enumerable.Range(0, count)
            .Select(_ => CreateSimpleTestOrder(customerId))
            .ToList();
    }
}