namespace CosmoBase.Abstractions.Configuration;

public class CosmosModelConfiguration
{
    public string ModelName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public string PartitionKey { get; set; } = string.Empty;
    
    // These must match one of the CosmosClientConfiguration.Name values
    public string ReadCosmosClientConfigurationName { get; set; } = string.Empty;
    public string WriteCosmosClientConfigurationName { get; set; } = string.Empty;
}