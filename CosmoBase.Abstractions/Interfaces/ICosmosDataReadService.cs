using CosmoBase.Abstractions.Filters;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Provides high-level read operations for Cosmos DB documents with automatic DTO/DAO mapping,
/// comprehensive querying capabilities, and performance optimizations. This service abstracts away
/// Cosmos DB implementation details and provides a clean domain-focused API for data retrieval operations.
/// </summary>
/// <typeparam name="T">The DTO type exposed to consumers that represents the domain model.</typeparam>
/// <remarks>
/// This service automatically handles:
/// - **Soft delete filtering**: Non-deleted documents are returned by default unless explicitly requested
/// - **DTO/DAO mapping**: Automatic conversion between storage objects (DAOs) and domain objects (DTOs)
/// - **Query optimization**: Intelligent query execution with proper indexing and partition scoping
/// - **Performance monitoring**: Request unit tracking and diagnostic information for cost optimization
/// 
/// All query operations respect the soft delete pattern and include comprehensive error handling,
/// logging, and telemetry for production monitoring and debugging.
/// </remarks>
public interface ICosmosDataReadService<T> : IDataReadService<T, string>
{
    /// <summary>
    /// Retrieves a single document by its unique identifier and partition key.
    /// </summary>
    /// <param name="id">The unique identifier of the document to retrieve.</param>
    /// <param name="partitionKey">The partition key value for the document.</param>
    /// <param name="includeDeleted">
    /// When <c>false</c> (default), returns <c>null</c> if the document is soft-deleted.
    /// When <c>true</c>, returns the document even if it's marked as deleted.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// The document if found and not soft-deleted (when <paramref name="includeDeleted"/> is <c>false</c>);
    /// the document if found regardless of deletion status (when <paramref name="includeDeleted"/> is <c>true</c>);
    /// otherwise <c>null</c>.
    /// </returns>
    Task<T?> GetByIdAsync(string id, string partitionKey, bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds documents where an array property contains an element with the specified property value.
    /// </summary>
    /// <param name="arrayName">The name of the array property to search within.</param>
    /// <param name="elementPropertyName">The name of the property within each array element to match.</param>
    /// <param name="elementPropertyValue">The value to match against the array element property.</param>
    /// <param name="includeDeleted">
    /// When <c>false</c> (default), excludes soft-deleted documents from results.
    /// When <c>true</c>, includes soft-deleted documents in the results.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A list of matching documents. When <paramref name="includeDeleted"/> is <c>false</c>, 
    /// only non-deleted documents are returned. When <c>true</c>, all matching documents 
    /// are returned regardless of deletion status.
    /// </returns>
    Task<List<T>> GetAllByArrayPropertyAsync(
        string arrayName,
        string elementPropertyName,
        object elementPropertyValue,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all documents that satisfy a set of property-based filter conditions.
    /// </summary>
    /// <param name="propertyFilters">
    /// The collection of property filters to apply. Multiple filters are combined with AND logic.
    /// </param>
    /// <param name="includeDeleted">
    /// When <c>false</c> (default), excludes soft-deleted documents from results.
    /// When <c>true</c>, includes soft-deleted documents in the results.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A list of matching documents. When <paramref name="includeDeleted"/> is <c>false</c>, 
    /// only non-deleted documents are returned. When <c>true</c>, all matching documents 
    /// are returned regardless of deletion status.
    /// </returns>
    Task<List<T>> GetAllByPropertyComparisonAsync(
        IEnumerable<PropertyFilter> propertyFilters,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams all non-deleted documents asynchronously across all partitions.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <returns>An async stream of documents that can be consumed with await foreach.</returns>
    new IAsyncEnumerable<T> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams all non-deleted documents within a specific partition asynchronously.
    /// </summary>
    /// <param name="partitionKey">The partition key to scope the query to a specific partition.</param>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <returns>An async stream of documents within the specified partition.</returns>
    IAsyncEnumerable<T> GetAllAsync(string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a subset of documents using offset and limit semantics for pagination scenarios.
    /// </summary>
    /// <param name="limit">Maximum number of items to request per server-side page from Cosmos DB.</param>
    /// <param name="offset">Number of items to skip before beginning to return results.</param>
    /// <param name="count">Total maximum number of items to yield from the stream.</param>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <returns>An async stream of documents after applying offset/limit/count constraints.</returns>
    IAsyncEnumerable<T> GetAllAsync(int limit, int offset, int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams results of a specification-based query with advanced filtering and sorting capabilities.
    /// </summary>
    /// <param name="specification">
    /// The query specification that defines the filtering, sorting, and projection logic.
    /// Must be a SQL-based specification for Cosmos DB compatibility.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <returns>An async stream of documents matching the specification criteria.</returns>
    new IAsyncEnumerable<T> QueryAsync(ISpecification<T> specification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-reads documents in batches to maximize throughput for high-volume data processing scenarios.
    /// </summary>
    /// <param name="specification">
    /// The query specification defining which documents to retrieve.
    /// Must be a SQL-based specification for Cosmos DB compatibility.
    /// </param>
    /// <param name="partitionKey">
    /// The partition key value to scope the query to a specific partition for optimal performance.
    /// </param>
    /// <param name="batchSize">
    /// Number of documents to retrieve per batch. Recommended range: 100-1000 depending on document size.
    /// </param>
    /// <param name="maxConcurrency">
    /// Maximum number of parallel requests to Cosmos DB. Recommended range: 10-50 depending on provisioned throughput.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the bulk read operation.</param>
    /// <returns>
    /// An async stream of document batches, where each batch contains a list of documents.
    /// </returns>
    IAsyncEnumerable<List<T>> BulkReadAsyncEnumerable(
        ISpecification<T> specification,
        string partitionKey,
        int batchSize = 100,
        int maxConcurrency = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of non-deleted documents in the specified partition.
    /// </summary>
    /// <param name="partitionKeyValue">The partition key value to count documents within.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The count of non-deleted documents in the specified partition.</returns>
    Task<int> GetCountAsync(string partitionKeyValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of all documents (including soft-deleted) in the specified partition.
    /// </summary>
    /// <param name="partitionKeyValue">The partition key value to count documents within.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The total count of all documents including soft-deleted ones in the specified partition.</returns>
    Task<int> GetTotalCountAsync(string partitionKeyValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of non-deleted documents in the specified partition with intelligent caching
    /// to optimize performance for frequently accessed counts.
    /// </summary>
    /// <param name="partitionKeyValue">The partition key value to count documents within.</param>
    /// <param name="cacheExpiryMinutes">
    /// Maximum age of cached data in minutes before forcing a fresh count query.
    /// Set to 0 to always bypass cache and get fresh count.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// The count of non-deleted documents in the partition. Returns cached value if available
    /// and not expired, otherwise performs a fresh COUNT query and updates the cache.
    /// </returns>
    Task<int> GetCountWithCacheAsync(
        string partitionKeyValue,
        int cacheExpiryMinutes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached count for a specific partition, forcing the next GetCountWithCacheAsync
    /// call to perform a fresh count query.
    /// </summary>
    /// <param name="partitionKeyValue">The partition key whose cached count should be invalidated.</param>
    void InvalidateCountCache(string partitionKeyValue);

    /// <summary>
    /// Retrieves a specific page of documents with a continuation token for efficient pagination.
    /// </summary>
    /// <param name="specification">
    /// The query specification defining which documents to retrieve and how to sort them.
    /// Must be a SQL-based specification for Cosmos DB compatibility.
    /// </param>
    /// <param name="partitionKey">
    /// The partition key value to scope the query to a specific partition for optimal performance.
    /// </param>
    /// <param name="pageSize">
    /// The number of documents to retrieve per page. Recommended range: 10-100 depending on document size.
    /// </param>
    /// <param name="continuationToken">
    /// The continuation token from the previous page (null for the first page).
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A tuple containing the documents for the current page and a continuation token for the next page.
    /// The continuation token will be null if this is the last page.
    /// </returns>
    Task<(IList<T> Items, string? ContinuationToken)> GetPageWithTokenAsync(
        ISpecification<T> specification,
        string partitionKey,
        int pageSize,
        string? continuationToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a page of documents along with the total count of all matching documents,
    /// combining pagination and count information in a single efficient operation.
    /// </summary>
    /// <param name="specification">
    /// The query specification defining which documents to retrieve and how to sort them.
    /// Must be a SQL-based specification for Cosmos DB compatibility.
    /// </param>
    /// <param name="partitionKey">
    /// The partition key value to scope the query to a specific partition for optimal performance.
    /// </param>
    /// <param name="pageSize">
    /// The number of documents to retrieve per page. Recommended range: 10-100 depending on document size.
    /// </param>
    /// <param name="continuationToken">
    /// The continuation token from the previous page (null for the first page).
    /// Total count is only calculated on the first page (when continuationToken is null).
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A tuple containing:
    /// - Items: The documents for the current page
    /// - ContinuationToken: Token for the next page (null if this is the last page)
    /// - TotalCount: Total count of matching documents (only calculated on first page)
    /// </returns>
    Task<(IList<T> Items, string? ContinuationToken, int? TotalCount)> GetPageWithTokenAndCountAsync(
        ISpecification<T> specification,
        string partitionKey,
        int pageSize,
        string? continuationToken = null,
        CancellationToken cancellationToken = default);
}