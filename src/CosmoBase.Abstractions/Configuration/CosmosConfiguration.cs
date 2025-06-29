using System.Collections.Generic;

namespace CosmoBase.Abstractions.Configuration;

public class CosmosConfiguration
{
    public List<CosmosClientConfiguration> CosmosClientConfigurations { get; set; } = new();
    public List<CosmosModelConfiguration> CosmosModelConfigurations { get; set; } = new();
}