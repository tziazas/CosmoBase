using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Cosmos;

namespace CosmoBase.Core.Configuration;

/// <summary>
/// Configuration options for CosmoBase.
/// </summary>
public class CosmoBaseOptions
{
    /// <summary>
    /// Gets or sets the Cosmos DB connection string.
    /// </summary>
    [Required(ErrorMessage = "ConnectionString is required")]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the database name.
    /// </summary>
    [Required(ErrorMessage = "DatabaseName is required")]
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default container name.
    /// </summary>
    public string? DefaultContainerName { get; set; }

    /// <summary>
    /// Gets or sets the maximum retry attempts for rate-limited requests.
    /// </summary>
    [Range(0, 10, ErrorMessage = "MaxRetryAttempts must be between 0 and 10")]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum retry wait time in seconds.
    /// </summary>
    [Range(1, 300, ErrorMessage = "MaxRetryWaitTimeInSeconds must be between 1 and 300")]
    public int MaxRetryWaitTimeInSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether bulk operations are enabled.
    /// </summary>
    public bool EnableBulkOperations { get; set; } = true;

    /// <summary>
    /// Gets or sets the batch size for bulk operations.
    /// </summary>
    [Range(1, 1000, ErrorMessage = "BulkOperationBatchSize must be between 1 and 1000")]
    public int BulkOperationBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the connection mode.
    /// </summary>
    public ConnectionMode? ConnectionMode { get; set; }

    /// <summary>
    /// Gets or sets the consistency level.
    /// </summary>
    public ConsistencyLevel? ConsistencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the property naming policy.
    /// </summary>
    public CosmosPropertyNamingPolicy? PropertyNamingPolicy { get; set; }

    /// <summary>
    /// Gets or sets whether to ignore null values in serialization.
    /// </summary>
    public bool IgnoreNullValues { get; set; } = true;

    /// <summary>
    /// Gets or sets the application name for monitoring.
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// Gets or sets whether throughput control is enabled.
    /// </summary>
    public bool EnableThroughputControl { get; set; } = false;

    /// <summary>
    /// Gets or sets the throughput configuration.
    /// </summary>
    public ThroughputOptions Throughput { get; set; } = new();

    /// <summary>
    /// Gets or sets container-specific configurations.
    /// </summary>
    public Dictionary<string, ContainerConfiguration> ContainerConfigurations { get; set; } = new();
}

/// <summary>
/// Throughput configuration options.
/// </summary>
public class ThroughputOptions
{
    /// <summary>
    /// Gets or sets the database throughput (RU/s).
    /// </summary>
    public int? DatabaseThroughput { get; set; }

    /// <summary>
    /// Gets or sets container-specific throughput settings.
    /// </summary>
    public Dictionary<string, int> ContainerThroughput { get; set; } = new();

    /// <summary>
    /// Gets or sets whether autoscale is enabled.
    /// </summary>
    public bool AutoscaleEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum autoscale throughput.
    /// </summary>
    public int? AutoscaleMaxThroughput { get; set; }
}

/// <summary>
/// Container-specific configuration.
/// </summary>
public class ContainerConfiguration
{
    /// <summary>
    /// Gets or sets the container name.
    /// </summary>
    [Required]
    public string ContainerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the partition key path.
    /// </summary>
    public string PartitionKeyPath { get; set; } = "/id";

    /// <summary>
    /// Gets or sets the throughput for this container.
    /// </summary>
    public int? Throughput { get; set; }

    /// <summary>
    /// Gets or sets the default TTL in seconds (-1 for disabled).
    /// </summary>
    public int DefaultTimeToLive { get; set; } = -1;

    /// <summary>
    /// Gets or sets the indexing policy.
    /// </summary>
    public IndexingPolicy? IndexingPolicy { get; set; }

    /// <summary>
    /// Gets or sets unique key policies.
    /// </summary>
    public List<UniqueKeyPolicy> UniqueKeyPolicies { get; set; } = new();
}