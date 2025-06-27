using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Tests.Fixtures;
using CosmoBase.Tests.TestModels;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CosmoBase.Tests.Integration.Services;

/// <summary>
/// Debug tests to understand what services are registered in the DI container
/// </summary>
[Collection("CosmoBase")]
public class ServiceRegistrationDebugTests : IClassFixture<CosmoBaseTestFixture>
{
    private readonly CosmoBaseTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ServiceRegistrationDebugTests(CosmoBaseTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void Debug_All_Registered_Services()
    {
        // Arrange & Act
        var serviceProvider = _fixture.ServiceProvider;
        var services = serviceProvider.GetServices<object>();

        // Get all registered services
        var registeredServices = serviceProvider.GetType()
            .GetProperty("ServiceCallSite", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(serviceProvider);

        // Log all registered service types
        _output.WriteLine("=== ALL REGISTERED SERVICES ===");
        
        // Try to get service descriptors (this approach may vary based on DI container)
        try
        {
            var field = serviceProvider.GetType().GetField("_services", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                var serviceCollection = field.GetValue(serviceProvider);
                _output.WriteLine($"Services collection type: {serviceCollection?.GetType()}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Could not access internal services: {ex.Message}");
        }

        // Test specific service lookups with correct DTO/DAO signatures
        _output.WriteLine("\n=== SPECIFIC SERVICE TESTS ===");
        
        TestServiceRegistration<ICosmosDataReadService<TestProduct, TestProductDao>>("ICosmosDataReadService<TestProduct, TestProductDao>");
        TestServiceRegistration<ICosmosDataWriteService<TestProduct, TestProductDao>>("ICosmosDataWriteService<TestProduct, TestProductDao>");
        
        TestServiceRegistration<ICosmosDataReadService<TestOrder, TestOrderDao>>("ICosmosDataReadService<TestOrder, TestOrderDao>");
        TestServiceRegistration<ICosmosDataWriteService<TestOrder, TestOrderDao>>("ICosmosDataWriteService<TestOrder, TestOrderDao>");
        
        // Test repository
        TestServiceRegistration<ICosmosRepository<TestProductDao>>("ICosmosRepository<TestProduct>");
        
        // Test user context
        TestServiceRegistration<IUserContext>("IUserContext");
        
        // Assert - at least user context should be registered
        var userContext = _fixture.GetService<IUserContext>();
        userContext.Should().NotBeNull("IUserContext should be registered");
    }

    private void TestServiceRegistration<T>(string serviceName)
    {
        try
        {
            var service = _fixture.GetService<T>();
            if (service != null)
            {
                _output.WriteLine($"✅ {serviceName}: {service.GetType().FullName}");
            }
            else
            {
                _output.WriteLine($"❌ {serviceName}: NOT REGISTERED");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"💥 {serviceName}: ERROR - {ex.Message}");
        }
    }

    [Fact]
    public void Debug_CosmoBase_Configuration()
    {
        // Test the configuration that was used
        var configuration = _fixture.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
        configuration.Should().NotBeNull();

        var cosmoBaseSection = configuration.GetSection("CosmoBase");
        cosmoBaseSection.Should().NotBeNull();

        var clientConfigs = cosmoBaseSection.GetSection("CosmosClientConfigurations");
        var modelConfigs = cosmoBaseSection.GetSection("CosmosModelConfigurations");

        _output.WriteLine("=== CONFIGURATION DEBUG ===");
        _output.WriteLine($"CosmoBase section exists: {cosmoBaseSection.Exists()}");
        _output.WriteLine($"Client configurations exist: {clientConfigs.Exists()}");
        _output.WriteLine($"Model configurations exist: {modelConfigs.Exists()}");

        // Log the configuration values
        foreach (var child in cosmoBaseSection.GetChildren())
        {
            _output.WriteLine($"Config key: {child.Key}");
            if (child.Key == "CosmosClientConfigurations")
            {
                foreach (var clientConfig in child.GetChildren())
                {
                    _output.WriteLine($"  Client: {clientConfig["Name"]}");
                }
            }
            if (child.Key == "CosmosModelConfigurations")
            {
                foreach (var modelConfig in child.GetChildren())
                {
                    _output.WriteLine($"  Model: {modelConfig["ModelName"]}");
                }
            }
        }
    }

    [Fact]
    public void Debug_AddCosmoBase_Call()
    {
        // This test verifies that AddCosmoBase was called correctly
        _output.WriteLine("=== ADD COSMO BASE DEBUG ===");
        
        // Check if the required dependencies for CosmoBase are present
        TestServiceRegistration<Microsoft.Extensions.Configuration.IConfiguration>("IConfiguration");
        TestServiceRegistration<Microsoft.Extensions.Logging.ILoggerFactory>("ILoggerFactory");
        TestServiceRegistration<Microsoft.Extensions.Logging.ILogger<CosmoBaseTestFixture>>("ILogger<CosmoBaseTestFixture>");
        
        _output.WriteLine("\nNote: If IConfiguration and ILoggerFactory are registered but CosmoBase services are not,");
        _output.WriteLine("then the issue is likely in how AddCosmoBase() is implemented or called.");
    }
}