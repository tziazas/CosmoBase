using System.ComponentModel.DataAnnotations;

namespace CosmoBase.Abstractions.Configuration;

/// <summary>
/// Configuration for a Cosmos DB client instance.
/// </summary>
public record CosmosClientConfiguration
{
    /// <summary>
    /// Gets or sets the unique name for this Cosmos client instance.
    /// </summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the Cosmos DB connection string.
    /// </summary>
    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of workers for parallel operations.
    /// </summary>
    [Range(1, 100)]
    public int NumberOfWorkers { get; init; }

    /// <summary>
    /// Gets or sets whether bulk operations are enabled. Defaults to true.
    /// </summary>
    public bool? AllowBulkExecution { get; init; }

    /// <summary>
    /// Gets or sets the connection mode. Valid values: "Direct", "Gateway". Defaults to "Direct".
    /// </summary>
    public string? ConnectionMode { get; init; }

    /// <summary>
    /// Gets or sets the maximum retry attempts on rate-limited requests. Defaults to 9.
    /// </summary>
    [Range(0, 20)]
    public int? MaxRetryAttempts { get; init; }

    /// <summary>
    /// Gets or sets the maximum retry wait time in seconds. Defaults to 30.
    /// </summary>
    [Range(1, 300)]
    public int? MaxRetryWaitTimeInSeconds { get; init; }
}