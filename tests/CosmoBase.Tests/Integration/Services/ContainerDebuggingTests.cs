using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Tests.Fixtures;
using CosmoBase.Tests.TestModels;
using Xunit.Abstractions;

namespace CosmoBase.Tests.Integration.Services;

[Collection("CosmoBase")]
    public class ContainerDebuggingTests : IClassFixture<CosmoBaseTestFixture>
    {
        private readonly CosmoBaseTestFixture _fixture;
        private readonly ITestOutputHelper _output;

        public ContainerDebuggingTests(CosmoBaseTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public void Debug_Container_Configuration()
        {
            // Check the configuration that's being used
            var config = _fixture.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            
            var modelConfigs = config.GetSection("CosmoBase:CosmosModelConfigurations");
            
            foreach (var modelConfig in modelConfigs.GetChildren())
            {
                var modelName = modelConfig["ModelName"];
                var partitionKey = modelConfig["PartitionKey"];
                var collectionName = modelConfig["CollectionName"];
                
                _output.WriteLine($"Model: {modelName}");
                _output.WriteLine($"  Collection: {collectionName}");
                _output.WriteLine($"  Partition Key: {partitionKey}");
                _output.WriteLine("");
            }
        }

        [Fact]
        public async Task Test_Different_Partition_Key_Values()
        {
            var writeService = _fixture.GetRequiredService<ICosmosDataWriteService<TestProduct, TestProductDao>>();
            
            // Test different partition key values to see if any work
            var partitionKeys = new[] { "electronics", "books", "test", "simple" };
            
            foreach (var pk in partitionKeys)
            {
                try
                {
                    var product = new TestProduct
                    {
                        Id = $"test-{pk}-{Guid.NewGuid():N}",
                        Name = $"Test {pk}",
                        Category = pk,
                        CustomerId = "test-customer",
                        Price = 10.00m
                    };

                    var result = await writeService.CreateAsync(product);
                    _output.WriteLine($"✅ Partition key '{pk}' works");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"❌ Partition key '{pk}' failed: {ex.Message}");
                }
            }
        }
    }