using System.Diagnostics.Metrics;

namespace CosmoBase.Core.Repositories;

/// <summary>
/// Use .NET’s built-in System.Diagnostics.Metrics API (available in .NET 6+) to emit both counters and histograms,
/// and then wire that up to whatever exporter the user would like (OpenTelemetry, Prometheus, Azure Monitor, etc.).
/// </summary>
internal static class CosmosRepositoryMetrics
{
    // Meter lives here once for the entire assembly
    public static readonly Meter Meter =
        new("CosmoBase.CosmosRepository", "1.0.0");

    // Histogram for RU charges
    public static readonly Histogram<double> RequestCharge =
        Meter.CreateHistogram<double>(
            "cosmos.request_charge",
            unit: "RU",
            description: "RUs consumed per Cosmos call");

    // Counter for retries
    public static readonly Counter<long> RetryCount =
        Meter.CreateCounter<long>(
            "cosmos.retry_count",
            unit: "count",
            description: "Number of Cosmos retries");
    
    // Counter for cache hits
    public static readonly Counter<long> CacheHitCount =
        Meter.CreateCounter<long>(
            "cosmos.cache_hit_count",
            unit: "count",
            description: "Number of cache hits for count queries");

    // Counter for cache misses
    public static readonly Counter<long> CacheMissCount =
        Meter.CreateCounter<long>(
            "cosmos.cache_miss_count",
            unit: "count",
            description: "Number of cache misses for count queries");
}