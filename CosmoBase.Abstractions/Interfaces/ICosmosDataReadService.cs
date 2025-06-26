using CosmoBase.Abstractions.Filters;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Cosmos‐DB‐specific read operations on <typeparamref name="T"/>.
/// </summary>
public interface ICosmosDataReadService<T> : IDataReadService<T, string>
{
    /// <summary>
    /// Bulk‐read in batches for maximum throughput.
    /// </summary>
    IAsyncEnumerable<List<T>> BulkReadAsyncEnumerable(
        ISpecification<T> specification,
        string partitionKey,
        int batchSize = 100,
        int maxConcurrency = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Count documents in a given partition.
    /// </summary>
    Task<int> GetCountAsync(
        string partitionKeyValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single page of items plus continuation token.
    /// </summary>
    Task<(IList<T> Items, string? ContinuationToken)> GetPageWithTokenAsync(
        ISpecification<T> specification,
        string partitionKey,
        int pageSize,
        string? continuationToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a page of items, continuation token, and total count.
    /// </summary>
    Task<(IList<T> Items, string? ContinuationToken, int? TotalCount)> GetPageWithTokenAndCountAsync(
        ISpecification<T> specification,
        string partitionKey,
        int pageSize,
        string? continuationToken = null,
        CancellationToken cancellationToken = default);
}