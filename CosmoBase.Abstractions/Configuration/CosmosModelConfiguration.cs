using System.ComponentModel.DataAnnotations;

namespace CosmoBase.Abstractions.Configuration;

public class CosmosModelConfiguration
{
    [Required] public string ModelName { get; set; } = string.Empty;
    [Required] public string DatabaseName { get; set; } = string.Empty;
    [Required] public string CollectionName { get; set; } = string.Empty;
    [Required] public string PartitionKey { get; set; } = string.Empty;

    // These must match one of the CosmosClientConfiguration.Name values
    [Required] public string ReadCosmosClientConfigurationName { get; set; } = string.Empty;
    [Required] public string WriteCosmosClientConfigurationName { get; set; } = string.Empty;
}