using System.ComponentModel.DataAnnotations;

namespace CosmoBase.Abstractions.Configuration;

public record CosmosClientConfiguration
{
    [Required] public string Name { get; init; } = string.Empty;
    [Required] public string ConnectionString { get; init; } = string.Empty;
    [Range(1, 100)] public int NumberOfWorkers { get; init; }
}