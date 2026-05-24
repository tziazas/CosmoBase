using CosmoBase.Abstractions.Configuration;
using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Core.Services;
using CosmoBase.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CosmoBase.Tests.Unit.DependencyInjection;

/// <summary>
/// Unit tests that verify AddCosmoBase registers (and does not register) the expected services.
/// These run without a live Cosmos emulator — the CosmosClient factory is lazy so it is never
/// invoked when only checking descriptor presence.
/// </summary>
public class ServiceRegistrationTests
{
    // Minimal configuration that satisfies CosmosConfigurationValidator.
    // CosmosClient construction is deferred (singleton factory), so the connection string
    // is never actually used during these tests.
    private static IServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());

        services.AddCosmoBase(cfg =>
        {
            cfg.CosmosClientConfigurations =
            [
                new CosmosClientConfiguration
                {
                    Name = "Primary",
                    ConnectionString =
                        "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMfZf4KhkFaWfeTA==;"
                }
            ];
            cfg.CosmosModelConfigurations =
            [
                new CosmosModelConfiguration
                {
                    ModelName = "TestModel",
                    DatabaseName = "TestDb",
                    CollectionName = "TestCol",
                    PartitionKey = "Category",
                    ReadCosmosClientConfigurationName = "Primary",
                    WriteCosmosClientConfigurationName = "Primary"
                }
            ];
        }, new SystemUserContext("system"));

        return services;
    }

    [Fact]
    public void AddCosmoBase_BuildsServiceProvider_WithoutThrowing()
    {
        var services = BuildServices();
        var act = () => services.BuildServiceProvider();
        act.Should().NotThrow();
    }

    [Fact]
    public void AddCosmoBase_RegistersUserContext()
    {
        var provider = BuildServices().BuildServiceProvider();
        provider.GetService<IUserContext>().Should().NotBeNull();
    }

    [Fact]
    public void AddCosmoBase_RegistersMemoryCache()
    {
        var provider = BuildServices().BuildServiceProvider();
        provider.GetService<IMemoryCache>().Should().NotBeNull();
    }

    [Fact]
    public void AddCosmoBase_RegistersOpenGenericItemMapper()
    {
        var services = BuildServices();
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IItemMapper<,>));
        descriptor.Should().NotBeNull("IItemMapper<,> open generic should be registered");
    }

    [Fact]
    public void AddCosmoBase_RegistersOpenGenericAuditFieldManager()
    {
        var services = BuildServices();
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditFieldManager<>));
        descriptor.Should().NotBeNull("IAuditFieldManager<> open generic should be registered");
    }

    [Fact]
    public void AddCosmoBase_RegistersOpenGenericValidator()
    {
        var services = BuildServices();
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICosmosValidator<>));
        descriptor.Should().NotBeNull("ICosmosValidator<> open generic should be registered");
    }

    [Fact]
    public void AddCosmoBase_RegistersOpenGenericRepository()
    {
        var services = BuildServices();
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICosmosRepository<>));
        descriptor.Should().NotBeNull("ICosmosRepository<> open generic should be registered");
    }

    [Fact]
    public void AddCosmoBase_RegistersOpenGenericReadService()
    {
        var services = BuildServices();
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICosmosDataReadService<,>));
        descriptor.Should().NotBeNull("ICosmosDataReadService<,> open generic should be registered");
    }

    [Fact]
    public void AddCosmoBase_RegistersOpenGenericWriteService()
    {
        var services = BuildServices();
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICosmosDataWriteService<,>));
        descriptor.Should().NotBeNull("ICosmosDataWriteService<,> open generic should be registered");
    }

    [Fact]
    public void AddCosmoBase_DoesNotRegisterPollyPolicy()
    {
        var services = BuildServices();

        // Verify no service descriptor whose implementation type comes from the Polly assembly
        // is present — Polly was previously registered as a dead singleton that nothing consumed.
        var pollyDescriptor = services.FirstOrDefault(d =>
            d.ImplementationType?.Assembly.GetName().Name == "Polly" ||
            d.ImplementationFactory != null &&
            d.ServiceType.Assembly.GetName().Name == "Polly");

        pollyDescriptor.Should().BeNull("Polly should not be registered — it was never used");
    }
}
