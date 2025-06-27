using Bogus;
using CosmoBase.Tests.TestModels;

namespace CosmoBase.Tests.Helpers;

/// <summary>
/// Builder for generating realistic test data using Bogus
/// </summary>
public static class TestDataBuilder
{
    private static readonly Faker Faker = new("en");

    /// <summary>
    /// Creates a single test product with realistic data
    /// </summary>
    public static TestProduct CreateTestProduct(string? category = null, string? id = null)
    {
        return new Faker<TestProduct>()
            .RuleFor(p => p.Id, f => id ?? f.Random.Guid().ToString())
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Category, f => category ?? f.PickRandom("electronics", "books", "clothing", "home", "sports"))
            .RuleFor(p => p.Price, f => f.Random.Decimal(1, 10000))
            .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
            .RuleFor(p => p.Tags, f => f.Make(3, () => f.Commerce.Categories(1)[0]))
            .RuleFor(p => p.IsActive, f => f.Random.Bool(0.8f))
            .RuleFor(p => p.StockQuantity, f => f.Random.Int(0, 1000))
            .RuleFor(p => p.Sku, f => f.Commerce.Ean13())
            .RuleFor(p => p.Barcode, f => f.Commerce.Ean8())
            .RuleFor(p => p.Metadata, f => CreateProductMetadata())
            .RuleFor(p => p.Dimensions, f => CreateProductDimensions())
            .RuleFor(p => p.DiscontinuedDate, f => f.Random.Bool(0.1f) ? f.Date.Past() : null)
            .RuleFor(p => p.Category, f => f.Commerce.Color())
            .Generate();
    }

    /// <summary>
    /// Creates product metadata with realistic data
    /// </summary>
    public static ProductMetadata CreateProductMetadata()
    {
        return new Faker<ProductMetadata>()
            .RuleFor(m => m.Brand, f => f.Company.CompanyName())
            .RuleFor(m => m.Model, f => f.Random.AlphaNumeric(8))
            .RuleFor(m => m.Color, f => f.Commerce.Color())
            .RuleFor(m => m.Size, f => f.PickRandom("XS", "S", "M", "L", "XL", "XXL"))
            .RuleFor(m => m.Weight, f => f.Random.Double(0.1, 50))
            .RuleFor(m => m.Material, f => f.PickRandom("Cotton", "Polyester", "Metal", "Plastic", "Wood", "Glass"))
            .RuleFor(m => m.Origin, f => f.Address.Country())
            .RuleFor(m => m.CustomAttributes, f => new Dictionary<string, object>
            {
                ["warranty"] = f.Random.Int(1, 36) + " months",
                ["rating"] = f.Random.Double(1, 5),
                ["reviews"] = f.Random.Int(0, 1000)
            })
            .Generate();
    }

    /// <summary>
    /// Creates product dimensions with realistic data
    /// </summary>
    public static ProductDimensions CreateProductDimensions()
    {
        return new Faker<ProductDimensions>()
            .RuleFor(d => d.Length, f => f.Random.Double(1, 100))
            .RuleFor(d => d.Width, f => f.Random.Double(1, 100))
            .RuleFor(d => d.Height, f => f.Random.Double(1, 100))
            .RuleFor(d => d.Unit, f => f.PickRandom("cm", "in", "mm"))
            .Generate();
    }

    /// <summary>
    /// Creates a single test order with realistic data
    /// </summary>
    public static TestOrder CreateTestOrder(string? customerId = null, string? id = null)
    {
        return new Faker<TestOrder>()
            .RuleFor(o => o.Id, f => id ?? f.Random.Guid().ToString())
            .RuleFor(o => o.CustomerId, f => customerId ?? f.Random.Guid().ToString())
            .RuleFor(o => o.OrderNumber, f => f.Random.AlphaNumeric(10).ToUpper())
            .RuleFor(o => o.OrderDate, f => f.Date.Recent(30))
            .RuleFor(o => o.Status, f => f.PickRandom<OrderStatus>())
            .RuleFor(o => o.TotalAmount, f => f.Random.Decimal(10, 5000))
            .RuleFor(o => o.ShippingCost, f => f.Random.Decimal(0, 50))
            .RuleFor(o => o.TaxAmount, f => f.Random.Decimal(1, 500))
            .RuleFor(o => o.DiscountAmount, f => f.Random.Decimal(0, 100))
            .RuleFor(o => o.Items, f => f.Make(f.Random.Int(1, 5), () => CreateOrderItem()))
            .RuleFor(o => o.ShippingAddress, f => CreateShippingAddress())
            .RuleFor(o => o.BillingAddress, f => CreateBillingAddress())
            .RuleFor(o => o.PaymentInfo, f => CreatePaymentInfo())
            .RuleFor(o => o.Notes, f => f.Random.Bool(0.3f) ? f.Lorem.Sentence() : null)
            .RuleFor(o => o.TrackingNumber, f => f.Random.Bool(0.5f) ? f.Random.AlphaNumeric(12) : null)
            .Generate();
    }

    /// <summary>
    /// Creates an order item with realistic data
    /// </summary>
    public static OrderItem CreateOrderItem()
    {
        return new Faker<OrderItem>()
            .RuleFor(i => i.ProductId, f => f.Random.Guid().ToString())
            .RuleFor(i => i.ProductName, f => f.Commerce.ProductName())
            .RuleFor(i => i.Sku, f => f.Commerce.Ean13())
            .RuleFor(i => i.Quantity, f => f.Random.Int(1, 10))
            .RuleFor(i => i.UnitPrice, f => f.Random.Decimal(1, 1000))
            .RuleFor(i => i.Attributes, f => new Dictionary<string, object>
            {
                ["color"] = f.Commerce.Color(),
                ["size"] = f.PickRandom("S", "M", "L", "XL")
            })
            .Generate();
    }

    /// <summary>
    /// Creates a shipping address with realistic data
    /// </summary>
    public static ShippingAddress CreateShippingAddress()
    {
        return new Faker<ShippingAddress>()
            .RuleFor(a => a.Name, f => f.Name.FullName())
            .RuleFor(a => a.AddressLine1, f => f.Address.StreetAddress())
            .RuleFor(a => a.AddressLine2, f => f.Random.Bool(0.3f) ? f.Address.SecondaryAddress() : null)
            .RuleFor(a => a.City, f => f.Address.City())
            .RuleFor(a => a.State, f => f.Address.State())
            .RuleFor(a => a.PostalCode, f => f.Address.ZipCode())
            .RuleFor(a => a.Country, f => f.Address.Country())
            .RuleFor(a => a.Phone, f => f.Phone.PhoneNumber())
            .Generate();
    }

    /// <summary>
    /// Creates a billing address with realistic data
    /// </summary>
    public static BillingAddress CreateBillingAddress()
    {
        return new Faker<BillingAddress>()
            .RuleFor(a => a.Name, f => f.Name.FullName())
            .RuleFor(a => a.AddressLine1, f => f.Address.StreetAddress())
            .RuleFor(a => a.AddressLine2, f => f.Random.Bool(0.3f) ? f.Address.SecondaryAddress() : null)
            .RuleFor(a => a.City, f => f.Address.City())
            .RuleFor(a => a.State, f => f.Address.State())
            .RuleFor(a => a.PostalCode, f => f.Address.ZipCode())
            .RuleFor(a => a.Country, f => f.Address.Country())
            .RuleFor(a => a.Phone, f => f.Phone.PhoneNumber())
            .RuleFor(a => a.Company, f => f.Random.Bool(0.4f) ? f.Company.CompanyName() : null)
            .Generate();
    }

    /// <summary>
    /// Creates payment info with realistic data
    /// </summary>
    public static PaymentInfo CreatePaymentInfo()
    {
        return new Faker<PaymentInfo>()
            .RuleFor(p => p.Method, f => f.PickRandom<PaymentMethod>())
            .RuleFor(p => p.TransactionId, f => f.Random.AlphaNumeric(16))
            .RuleFor(p => p.PaymentReference, f => f.Random.AlphaNumeric(20))
            .RuleFor(p => p.ProcessedDate, f => f.Date.Recent(7))
            .RuleFor(p => p.Status, f => f.PickRandom<PaymentStatus>())
            .Generate();
    }

    /// <summary>
    /// Creates multiple test products
    /// </summary>
    public static List<TestProduct> CreateTestProducts(int count, string? category = null)
    {
        return Enumerable.Range(0, count)
            .Select(_ => CreateTestProduct(category))
            .ToList();
    }

    /// <summary>
    /// Creates multiple test orders
    /// </summary>
    public static List<TestOrder> CreateTestOrders(int count, string? customerId = null)
    {
        return Enumerable.Range(0, count)
            .Select(_ => CreateTestOrder(customerId))
            .ToList();
    }

    /// <summary>
    /// Creates a product with specific validation issues for testing
    /// </summary>
    public static TestProduct CreateInvalidProduct()
    {
        return new TestProduct
        {
            // Missing required Name and Category
            Price = -10, // Invalid price
            Description = new string('x', 600), // Too long description
            StockQuantity = -5 // Invalid stock quantity
        };
    }

    /// <summary>
    /// Creates an order with specific validation issues for testing
    /// </summary>
    public static TestOrder CreateInvalidOrder()
    {
        return new TestOrder
        {
            // Missing required CustomerId and OrderNumber
            TotalAmount = -100, // Invalid amount
            Items = new List<OrderItem>
            {
                new OrderItem
                {
                    // Missing required fields
                    Quantity = 0, // Invalid quantity
                    UnitPrice = -50 // Invalid price
                }
            }
        };
    }

    /// <summary>
    /// Creates test data with specific categories for testing partitioning
    /// </summary>
    public static class Categories
    {
        public static readonly string[] Electronics = { "electronics" };
        public static readonly string[] Books = { "books" };
        public static readonly string[] Clothing = { "clothing" };
        public static readonly string[] Home = { "home" };
        public static readonly string[] Sports = { "sports" };
        public static readonly string[] All = { "electronics", "books", "clothing", "home", "sports" };
    }

    /// <summary>
    /// Creates test data with specific customer IDs for testing partitioning
    /// </summary>
    public static class CustomerIds
    {
        public static readonly string Customer1 = "customer-1";
        public static readonly string Customer2 = "customer-2";
        public static readonly string Customer3 = "customer-3";
        public static readonly string[] All = { Customer1, Customer2, Customer3 };
    }
}