// ============================================================================
// Fix 1: Update TestDataBuilder to ensure UTF-8 safe data generation
// ============================================================================

using Bogus;
using CosmoBase.Tests.TestModels;
using System.Text;

namespace CosmoBase.Tests.Helpers;

/// <summary>
/// Builder for generating realistic test data using Bogus with UTF-8 safety
/// </summary>
public static class TestDataBuilder
{
    /// <summary>
    /// Ensures a string is UTF-8 safe by removing invalid characters
    /// </summary>
    private static string EnsureUtf8Safe(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        // Convert to bytes and back to remove invalid UTF-8 sequences
        var bytes = Encoding.UTF8.GetBytes(input);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Creates a single test product with UTF-8 safe realistic data
    /// </summary>
    public static TestProduct CreateTestProduct(string? category = null, string? id = null)
    {
        return new Faker<TestProduct>()
            .RuleFor(p => p.Id, f => id ?? f.Random.Guid().ToString())
            .RuleFor(p => p.Name, f => EnsureUtf8Safe(f.Commerce.ProductName()))
            .RuleFor(p => p.Category, f => category ?? f.PickRandom("electronics", "books", "clothing", "home", "sports"))
            .RuleFor(p => p.CustomerId, f => f.Random.Guid().ToString()) // Fixed: was missing this assignment
            .RuleFor(p => p.Price, f => Math.Round(f.Random.Decimal(1, 10000), 2))
            .RuleFor(p => p.Description, f => EnsureUtf8Safe(f.Commerce.ProductDescription()))
            .RuleFor(p => p.Tags, f => f.Make(3, () => EnsureUtf8Safe(f.Commerce.Categories(1)[0])))
            .RuleFor(p => p.IsActive, f => f.Random.Bool(0.8f))
            .RuleFor(p => p.StockQuantity, f => f.Random.Int(0, 1000))
            .RuleFor(p => p.Sku, f => f.Random.AlphaNumeric(12)) // Use alphanumeric instead of EAN13
            .RuleFor(p => p.Barcode, f => f.Random.AlphaNumeric(8)) // Use alphanumeric instead of EAN8
            .RuleFor(p => p.Metadata, _ => CreateProductMetadata())
            .RuleFor(p => p.Dimensions, _ => CreateProductDimensions())
            .RuleFor(p => p.DiscontinuedDate, f => f.Random.Bool(0.1f) ? f.Date.Past() : null)
            // Remove the duplicate Category rule that was overriding with Color
            .Generate();
    }

    /// <summary>
    /// Creates product metadata with UTF-8 safe realistic data
    /// </summary>
    public static ProductMetadata CreateProductMetadata()
    {
        return new Faker<ProductMetadata>()
            .RuleFor(m => m.Brand, f => EnsureUtf8Safe(f.Company.CompanyName()))
            .RuleFor(m => m.Model, f => f.Random.AlphaNumeric(8))
            .RuleFor(m => m.Color, f => EnsureUtf8Safe(f.Commerce.Color()))
            .RuleFor(m => m.Size, f => f.PickRandom("XS", "S", "M", "L", "XL", "XXL"))
            .RuleFor(m => m.Weight, f => Math.Round(f.Random.Double(0.1, 50), 2))
            .RuleFor(m => m.Material, f => f.PickRandom("Cotton", "Polyester", "Metal", "Plastic", "Wood", "Glass"))
            .RuleFor(m => m.Origin, f => EnsureUtf8Safe(f.Address.Country()))
            .RuleFor(m => m.CustomAttributes, f => new Dictionary<string, object>
            {
                ["warranty"] = f.Random.Int(1, 36) + " months",
                ["rating"] = Math.Round(f.Random.Double(1, 5), 1),
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
            .RuleFor(d => d.Length, f => Math.Round(f.Random.Double(1, 100), 2))
            .RuleFor(d => d.Width, f => Math.Round(f.Random.Double(1, 100), 2))
            .RuleFor(d => d.Height, f => Math.Round(f.Random.Double(1, 100), 2))
            .RuleFor(d => d.Unit, f => f.PickRandom("cm", "in", "mm"))
            .Generate();
    }

    /// <summary>
    /// Creates a single test order with UTF-8 safe realistic data
    /// </summary>
    public static TestOrder CreateTestOrder(string? customerId = null, string? id = null)
    {
        return new Faker<TestOrder>()
            .RuleFor(o => o.Id, f => id ?? f.Random.Guid().ToString())
            .RuleFor(o => o.CustomerId, f => customerId ?? f.Random.Guid().ToString())
            .RuleFor(o => o.OrderNumber, f => f.Random.AlphaNumeric(10).ToUpper())
            .RuleFor(o => o.OrderDate, f => f.Date.Recent(30))
            .RuleFor(o => o.Status, f => f.PickRandom<OrderStatus>())
            .RuleFor(o => o.TotalAmount, f => Math.Round(f.Random.Decimal(10, 5000), 2))
            .RuleFor(o => o.ShippingCost, f => Math.Round(f.Random.Decimal(0, 50), 2))
            .RuleFor(o => o.TaxAmount, f => Math.Round(f.Random.Decimal(1, 500), 2))
            .RuleFor(o => o.DiscountAmount, f => Math.Round(f.Random.Decimal(0, 100), 2))
            .RuleFor(o => o.Items, f => f.Make(f.Random.Int(1, 5), CreateOrderItem))
            .RuleFor(o => o.ShippingAddress, _ => CreateShippingAddress())
            .RuleFor(o => o.BillingAddress, _ => CreateBillingAddress())
            .RuleFor(o => o.PaymentInfo, _ => CreatePaymentInfo())
            .RuleFor(o => o.Notes, f => f.Random.Bool(0.3f) ? EnsureUtf8Safe(f.Lorem.Sentence()) : null)
            .RuleFor(o => o.TrackingNumber, f => f.Random.Bool(0.5f) ? f.Random.AlphaNumeric(12) : null)
            .Generate();
    }

    /// <summary>
    /// Creates an order item with UTF-8 safe realistic data
    /// </summary>
    public static OrderItem CreateOrderItem()
    {
        return new Faker<OrderItem>()
            .RuleFor(i => i.ProductId, f => f.Random.Guid().ToString())
            .RuleFor(i => i.ProductName, f => EnsureUtf8Safe(f.Commerce.ProductName()))
            .RuleFor(i => i.Sku, f => f.Random.AlphaNumeric(13))
            .RuleFor(i => i.Quantity, f => f.Random.Int(1, 10))
            .RuleFor(i => i.UnitPrice, f => Math.Round(f.Random.Decimal(1, 1000), 2))
            .RuleFor(i => i.Attributes, f => new Dictionary<string, object>
            {
                ["color"] = EnsureUtf8Safe(f.Commerce.Color()),
                ["size"] = f.PickRandom("S", "M", "L", "XL")
            })
            .Generate();
    }

    /// <summary>
    /// Creates a shipping address with UTF-8 safe realistic data
    /// </summary>
    public static ShippingAddress CreateShippingAddress()
    {
        return new Faker<ShippingAddress>()
            .RuleFor(a => a.Name, f => EnsureUtf8Safe(f.Name.FullName()))
            .RuleFor(a => a.AddressLine1, f => EnsureUtf8Safe(f.Address.StreetAddress()))
            .RuleFor(a => a.AddressLine2, f => f.Random.Bool(0.3f) ? EnsureUtf8Safe(f.Address.SecondaryAddress()) : null)
            .RuleFor(a => a.City, f => EnsureUtf8Safe(f.Address.City()))
            .RuleFor(a => a.State, f => EnsureUtf8Safe(f.Address.State()))
            .RuleFor(a => a.PostalCode, f => f.Random.Replace("#####"))
            .RuleFor(a => a.Country, _ => "United States") // Use simple ASCII country name
            .RuleFor(a => a.Phone, f => f.Random.Replace("###-###-####"))
            .Generate();
    }

    /// <summary>
    /// Creates a billing address with UTF-8 safe realistic data
    /// </summary>
    public static BillingAddress CreateBillingAddress()
    {
        return new Faker<BillingAddress>()
            .RuleFor(a => a.Name, f => EnsureUtf8Safe(f.Name.FullName()))
            .RuleFor(a => a.AddressLine1, f => EnsureUtf8Safe(f.Address.StreetAddress()))
            .RuleFor(a => a.AddressLine2, f => f.Random.Bool(0.3f) ? EnsureUtf8Safe(f.Address.SecondaryAddress()) : null)
            .RuleFor(a => a.City, f => EnsureUtf8Safe(f.Address.City()))
            .RuleFor(a => a.State, f => EnsureUtf8Safe(f.Address.State()))
            .RuleFor(a => a.PostalCode, f => f.Random.Replace("#####"))
            .RuleFor(a => a.Country, _ => "United States") // Use simple ASCII country name
            .RuleFor(a => a.Phone, f => f.Random.Replace("###-###-####"))
            .RuleFor(a => a.Company, f => f.Random.Bool(0.4f) ? EnsureUtf8Safe(f.Company.CompanyName()) : null)
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

    // Rest of the methods remain the same...
    public static List<TestProduct> CreateTestProducts(int count, string? category = null)
    {
        return Enumerable.Range(0, count)
            .Select(_ => CreateTestProduct(category))
            .ToList();
    }

    public static List<TestOrder> CreateTestOrders(int count, string? customerId = null)
    {
        return Enumerable.Range(0, count)
            .Select(_ => CreateTestOrder(customerId))
            .ToList();
    }

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

    public static TestOrder CreateInvalidOrder()
    {
        return new TestOrder
        {
            // Missing required CustomerId and OrderNumber
            TotalAmount = -100, // Invalid amount
            Items =
            [
                new OrderItem
                {
                    // Missing required fields
                    Quantity = 0, // Invalid quantity
                    UnitPrice = -50 // Invalid price
                }
            ]
        };
    }

    public static class Categories
    {
        public static readonly string[] Electronics = ["electronics"];
        public static readonly string[] Books = ["books"];
        public static readonly string[] Clothing = ["clothing"];
        public static readonly string[] Home = ["home"];
        public static readonly string[] Sports = ["sports"];
        public static readonly string[] All = ["electronics", "books", "clothing", "home", "sports"];
    }

    public static class CustomerIds
    {
        public static readonly string Customer1 = "customer-1";
        public static readonly string Customer2 = "customer-2";
        public static readonly string Customer3 = "customer-3";
        public static readonly string[] All = [Customer1, Customer2, Customer3];
    }
}