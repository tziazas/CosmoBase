namespace CosmoBase.Abstractions.Configuration;

public record CosmosClientConfiguration(string Name, string ConnectionString, int NumberOfWorkers);