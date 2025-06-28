using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Tests.Fixtures;
using CosmoBase.Tests.TestModels;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CosmoBase.Tests.Integration.Services;

/// <summary>
/// Debug tests to understand what services are registered in the DI container
/// </summary>
[Collection("CosmoBase")]
public class ServiceRegistrationDebugTests(CosmoBaseTestFixture fixture, ITestOutputHelper output)
    : IClassFixture<CosmoBaseTestFixture>
{
    [Fact]
    public void Debug_All_Registered_Services()
    {
        // Arrange & Act
        var serviceProvider = fixture.ServiceProvider;
        serviceProvider.GetServices<object>();

        // Get all registered services
        var registeredServices = serviceProvider.GetType()
            .GetProperty("ServiceCallSite",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(serviceProvider);

        // Log all registered service types
        output.WriteLine("=== ALL REGISTERED SERVICES ===");

        // Try to get service descriptors (this approach may vary based on DI container)
        try
        {
            var field = serviceProvider.GetType().GetField("_services",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                var serviceCollection = field.GetValue(serviceProvider);
                output.WriteLine($"Services collection type: {serviceCollection?.GetType()}");
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"Could not access internal services: {ex.Message}");
        }

        // Test specific service lookups with correct DTO/DAO signatures
        output.WriteLine("\n=== SPECIFIC SERVICE TESTS ===");

        TestServiceRegistration<ICosmosDataReadService<TestProduct, TestProductDao>>(
            "ICosmosDataReadService<TestProduct, TestProductDao>");
        TestServiceRegistration<ICosmosDataWriteService<TestProduct, TestProductDao>>(
            "ICosmosDataWriteService<TestProduct, TestProductDao>");

        TestServiceRegistration<ICosmosDataReadService<TestOrder, TestOrderDao>>(
            "ICosmosDataReadService<TestOrder, TestOrderDao>");
        TestServiceRegistration<ICosmosDataWriteService<TestOrder, TestOrderDao>>(
            "ICosmosDataWriteService<TestOrder, TestOrderDao>");

        // Test repository
        TestServiceRegistration<ICosmosRepository<TestProductDao>>("ICosmosRepository<TestProduct>");

        // Test user context
        TestServiceRegistration<IUserContext>("IUserContext");

        // Assert - at least user context should be registered
        var userContext = fixture.GetService<IUserContext>();
        userContext.Should().NotBeNull("IUserContext should be registered");
    }

    private void TestServiceRegistration<T>(string serviceName)
    {
        try
        {
            var service = fixture.GetService<T>();
            output.WriteLine(service != null
                ? $"✅ {serviceName}: {service.GetType().FullName}"
                : $"❌ {serviceName}: NOT REGISTERED");
        }
        catch (Exception ex)
        {
            output.WriteLine($"💥 {serviceName}: ERROR - {ex.Message}");
        }
    }

    [Fact]
    public void Debug_CosmoBase_Configuration()
    {
        // Test the configuration that was used
        var configuration = fixture.GetService<IConfiguration>();
        configuration.Should().NotBeNull();

        var cosmoBaseSection = configuration.GetSection("CosmoBase");
        cosmoBaseSection.Should().NotBeNull();

        var clientConfigs = cosmoBaseSection.GetSection("CosmosClientConfigurations");
        var modelConfigs = cosmoBaseSection.GetSection("CosmosModelConfigurations");

        output.WriteLine("=== CONFIGURATION DEBUG ===");
        output.WriteLine($"CosmoBase section exists: {cosmoBaseSection.Exists()}");
        output.WriteLine($"Client configurations exist: {clientConfigs.Exists()}");
        output.WriteLine($"Model configurations exist: {modelConfigs.Exists()}");

        // Log the configuration values
        foreach (var child in cosmoBaseSection.GetChildren())
        {
            output.WriteLine($"Config key: {child.Key}");
            if (child.Key == "CosmosClientConfigurations")
            {
                foreach (var clientConfig in child.GetChildren())
                {
                    output.WriteLine($"  Client: {clientConfig["Name"]}");
                }
            }

            if (child.Key == "CosmosModelConfigurations")
            {
                foreach (var modelConfig in child.GetChildren())
                {
                    output.WriteLine($"  Model: {modelConfig["ModelName"]}");
                }
            }
        }
    }

    [Fact]
    public Task Test_CosmoBaseTestFixture_Initialization()
    {
        // This test will automatically initialize the fixture through the constructor
        // and the IClassFixture<CosmoBaseTestFixture> mechanism

        output.WriteLine("=== FIXTURE INITIALIZATION TEST ===");

        // Test that the fixture initialized without throwing
        var serviceProvider = fixture.ServiceProvider;
        serviceProvider.Should().NotBeNull("ServiceProvider should be initialized");

        // Test that basic services are available
        var config = fixture.GetService<IConfiguration>();
        config.Should().NotBeNull("Configuration should be available");

        var logger = fixture.GetService<ILogger<CosmoBaseTestFixture>>();
        logger.Should().NotBeNull("Logger should be available");

        output.WriteLine("✅ Fixture initialization completed successfully");
        return Task.CompletedTask;
    }

    [Fact]
    public void Debug_AddCosmoBase_Call()
    {
        // This test verifies that AddCosmoBase was called correctly
        output.WriteLine("=== ADD COSMO BASE DEBUG ===");

        // Check if the required dependencies for CosmoBase are present
        TestServiceRegistration<IConfiguration>("IConfiguration");
        TestServiceRegistration<ILoggerFactory>("ILoggerFactory");
        TestServiceRegistration<ILogger<CosmoBaseTestFixture>>(
            "ILogger<CosmoBaseTestFixture>");

        output.WriteLine(
            "\nNote: If IConfiguration and ILoggerFactory are registered but CosmoBase services are not,");
        output.WriteLine("then the issue is likely in how AddCosmoBase() is implemented or called.");
    }
}