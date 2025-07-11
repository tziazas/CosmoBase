using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Filters;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Provides low-level CRUD and query capabilities against a Cosmos DB container with comprehensive 
/// audit field management, validation, and caching capabilities.
/// This repository should not be injected directly into API or client code—
/// use a higher-level service to encapsulate business logic and DTO mapping.
/// </summary>
/// <typeparam name="T">
/// The document model type stored in Cosmos (must implement <see cref="ICosmosDataModel"/>).
/// </typeparam>
/// <remarks>
/// This repository automatically manages audit fields for all operations:
/// - <c>CreatedOnUtc</c>, <c>UpdatedOnUtc</c>: Automatically set based on operation type
/// - <c>CreatedBy</c>, <c>UpdatedBy</c>: Populated from the configured user context
/// - <c>Deleted</c>: Managed for soft-delete operations
/// 
/// All write operations include comprehensive validation, retry policies, and telemetry.
/// </remarks>
public interface ICosmosRepository<T> : IDisposable
    where T : class, ICosmosDataModel
{
    /// <summary>
    /// Exposes an <see cref="IQueryable"/> over the container for custom LINQ queries.
    /// </summary>
    IQueryable<T> Queryable { get; }

    /// <summary>
    /// Retrieves a single document by its id and partition key.
    /// </summary>
    /// <param name="id">The document's unique identifier.</param>
    /// <param name="partitionKey">The value of the partition key for this document.</param>
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
    /// <remarks>
    /// This method respects the soft delete pattern by default. Set <paramref name="includeDeleted"/> to <c>true</c>
    /// only when you specifically need to access soft-deleted documents (e.g., for audit trails or recovery operations).
    /// </remarks>
    Task<T?> GetItemAsync(string id, string partitionKey, bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds documents where an array property contains an element with the specified property value.
    /// </summary>
    /// <param name="arrayName">The name of the array property.</param>
    /// <param name="elementPropertyName">The name of the property within each array element.</param>
    /// <param name="elementPropertyValue">The value to match in the array elements.</param>
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
    /// <remarks>
    /// This method uses Cosmos DB's ARRAY_CONTAINS function for efficient array querying.
    /// The soft delete filter is applied in addition to the array property match.
    /// </remarks>
    Task<List<T>> GetAllByArrayPropertyAsync(string arrayName, string elementPropertyName, object elementPropertyValue,
        bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all documents that satisfy a set of property filters.
    /// </summary>
    /// <param name="propertyFilters">The list of property filters to apply.</param>
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
    /// <remarks>
    /// The property filters are combined with AND logic. The soft delete filter 
    /// (when <paramref name="includeDeleted"/> is <c>false</c>) is applied as an additional 
    /// AND condition to the user-specified filters.
    /// </remarks>
    Task<List<T>> GetAllByPropertyComparisonAsync(IEnumerable<PropertyFilter> propertyFilters,
        bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new document with automatic audit field management. Throws if a document with the same id already exists.
    /// </summary>
    /// <param name="item">The document to create.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created document with populated audit fields.</returns>
    /// <remarks>
    /// Automatically sets the following audit fields:
    /// - <c>CreatedOnUtc</c>: Current UTC timestamp
    /// - <c>UpdatedOnUtc</c>: Current UTC timestamp
    /// - <c>CreatedBy</c>: Current user from user context
    /// - <c>UpdatedBy</c>: Current user from user context
    /// - <c>Deleted</c>: false
    /// 
    /// The operation includes comprehensive validation, retry policies, and automatic cache invalidation.
    /// </remarks>
    Task<T> CreateItemAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing document with automatic audit field management. Throws if the document does not exist.
    /// </summary>
    /// <param name="item">The document to replace.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The replaced document with updated audit fields.</returns>
    /// <remarks>
    /// Automatically updates the following audit fields while preserving creation information:
    /// - <c>UpdatedOnUtc</c>: Current UTC timestamp
    /// - <c>UpdatedBy</c>: Current user from user context
    /// - <c>CreatedOnUtc</c>, <c>CreatedBy</c>: Preserved from original document
    /// 
    /// If <c>CreatedOnUtc</c> is not set (edge case), it will be populated with current timestamp.
    /// The operation includes comprehensive validation and retry policies.
    /// </remarks>
    Task<T> ReplaceItemAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces a document with intelligent audit field management (upsert semantics).
    /// </summary>
    /// <param name="item">The document to upsert.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The upsert-ed document with appropriate audit fields.</returns>
    /// <remarks>
    /// Intelligently manages audit fields based on whether this is a create or update operation:
    /// 
    /// **For new documents (create):**
    /// - <c>CreatedOnUtc</c>: Current UTC timestamp
    /// - <c>UpdatedOnUtc</c>: Current UTC timestamp
    /// - <c>CreatedBy</c>: Current user from user context
    /// - <c>UpdatedBy</c>: Current user from user context
    /// - <c>Deleted</c>: false
    /// 
    /// **For existing documents (update):**
    /// - <c>UpdatedOnUtc</c>: Current UTC timestamp
    /// - <c>UpdatedBy</c>: Current user from user context
    /// - <c>CreatedOnUtc</c>, <c>CreatedBy</c>: Preserved from original values
    /// 
    /// The operation automatically invalidates count cache only for create operations (HTTP 201).
    /// Includes comprehensive validation and retry policies.
    /// </remarks>
    Task<T> UpsertItemAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document by id and partition key with automatic audit field management for soft deletes.
    /// </summary>
    /// <param name="id">The document's id.</param>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="deleteOptions">
    /// Options controlling soft-vs-hard delete behavior, TTL, etc.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <remarks>
    /// **For soft deletes (<c>DeleteOptions.SoftDelete</c>):**
    /// - <c>Deleted</c>: Set to true
    /// - <c>UpdatedOnUtc</c>: Current UTC timestamp
    /// - <c>UpdatedBy</c>: Current user from user context
    /// - Document remains in container but is filtered from standard queries
    /// 
    /// **For hard deletes (<c>DeleteOptions.HardDelete</c>):**
    /// - Document is permanently removed from the container
    /// - No audit field updates (document no longer exists)
    /// 
    /// Both operations automatically invalidate count cache and include retry policies.
    /// </remarks>
    Task DeleteItemAsync(string id, string partitionKey,
        DeleteOptions deleteOptions = DeleteOptions.HardDelete,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams all documents (non-deleted) asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <returns>An async stream of documents.</returns>
    IAsyncEnumerable<T> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams all documents within a specific partition asynchronously.
    /// </summary>
    /// <param name="partitionKey">The partition key to scope the query.</param>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <returns>An async stream of documents.</returns>
    IAsyncEnumerable<T> GetAllAsync(string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a subset of documents by offset, limit (page size), and total count cap.
    /// </summary>
    /// <param name="limit">Max items to request per server-side page.</param>
    /// <param name="offset">Number of items to skip before streaming.</param>
    /// <param name="count">Total number of items to yield.</param>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <returns>An async stream of documents after offset/limit/count.</returns>
    IAsyncEnumerable<T> GetAllAsync(int limit, int offset, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams results of a specification-based query.
    /// </summary>
    /// <param name="specification">The query specification.</param>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <returns>An async stream of documents matching the specification.</returns>
    IAsyncEnumerable<T> QueryAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-reads items in batches to maximize throughput.
    /// </summary>
    /// <param name="specification">The query specification.</param>
    /// <param name="partitionKey">Partition key value for the query.</param>
    /// <param name="batchSize">Number of items per batch/page.</param>
    /// <param name="maxConcurrency">Maximum parallel requests.</param>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <returns>An async stream of item batches.</returns>
    IAsyncEnumerable<List<T>> BulkReadAsyncEnumerable(ISpecification<T> specification, string partitionKey,
        int batchSize = 100, int maxConcurrency = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of documents in the specified partition.
    /// Excludes deleted items
    /// </summary>
    /// <param name="partitionKey">Partition key value.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The document count without deleted items.</returns>
    Task<int> GetCountAsync(string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of documents in the specified partition.
    /// Includes deleted items
    /// </summary>
    /// <param name="partitionKey">Partition key value.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The total document count including deleted items.</returns>
    Task<int> GetTotalCountAsync(string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of non-deleted documents in the specified partition with intelligent caching.
    /// This method caches the count result to avoid expensive repeated COUNT queries, but will
    /// refresh the cache if the data is older than the specified threshold.
    /// </summary>
    /// <param name="partitionKey">The partition key value to count documents in.</param>
    /// <param name="cacheExpiryMinutes">
    /// Maximum age of cached data in minutes before forcing a fresh count.
    /// Set to 0 to always bypass cache and get fresh count.
    /// Recommended values: 5-15 minutes for frequently changing data, 30-60 minutes for stable data.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// The count of non-deleted documents in the partition. Returns cached value if available
    /// and not expired, otherwise performs a fresh COUNT query and updates the cache.
    /// </returns>
    /// <remarks>
    /// This method is ideal for scenarios like:
    /// - Dashboard displays that don't need real-time accuracy
    /// - Pagination where approximate counts are acceptable
    /// - Frequently accessed partition sizes that change infrequently
    /// 
    /// Cache keys are scoped by model type and partition key, so different document types
    /// and partitions maintain separate cached counts.
    /// 
    /// Cache is automatically invalidated by write operations (create, delete, bulk operations).
    /// 
    /// Performance considerations:
    /// - Fresh COUNT queries consume RUs proportional to partition size
    /// - Cached results have near-zero RU cost
    /// - Memory usage scales with number of unique partition keys cached
    /// </remarks>
    Task<int> GetCountWithCacheAsync(
        string partitionKey,
        int cacheExpiryMinutes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached count for a specific partition.
    /// This method should be called after external operations that may affect document counts,
    /// or when you need to force a fresh count on the next GetCountWithCacheAsync call.
    /// </summary>
    /// <param name="partitionKey">The partition key whose cached count should be invalidated.</param>
    /// <remarks>
    /// The repository automatically invalidates cache for standard CRUD operations,
    /// but you may need to call this manually if:
    /// - External processes modify documents in this partition
    /// - You perform direct Cosmos operations outside this repository
    /// - You want to force fresh counts for critical operations
    /// </remarks>
    void InvalidateCountCache(string partitionKey);

    /// <summary>
    /// Fetches a specific page of items plus a continuation token for next page.
    /// </summary>
    /// <param name="specification">The query specification.</param>
    /// <param name="partitionKey">Partition key value.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="continuationToken">Token from previous page (null for first page).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Items and next continuation token.</returns>
    Task<(IList<T> Items, string? ContinuationToken)> GetPageWithTokenAsync(ISpecification<T> specification,
        string partitionKey, int pageSize, string? continuationToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a page of items, next continuation token, and total count in one call.
    /// </summary>
    /// <param name="specification">The query specification.</param>
    /// <param name="partitionKey">Partition key value.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="continuationToken">Token from previous page (null for first page).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Items, next continuation token, and total count.</returns>
    Task<(IList<T> Items, string? ContinuationToken, int? TotalCount)> GetPageWithTokenAndCountAsync(
        ISpecification<T> specification, string partitionKey, int pageSize, string? continuationToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert multiple documents in batches with comprehensive audit field management and error handling.
    /// </summary>
    /// <param name="items">Documents to upsert.</param>
    /// <param name="partitionKeyValue">Partition key value for all items.</param>
    /// <param name="batchSize">Max items per batch.</param>
    /// <param name="maxConcurrency">Max parallel requests.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <remarks>
    /// Each document in the batch receives appropriate audit field management:
    /// - **New documents**: All audit fields set (CreatedOnUtc, UpdatedOnUtc, CreatedBy, UpdatedBy, Deleted = false)
    /// - **Existing documents**: Update fields set (UpdatedOnUtc, UpdatedBy), creation fields preserved
    /// 
    /// Features:
    /// - Parallel processing with configurable concurrency limits
    /// - Comprehensive error handling with item-level failure details
    /// - Automatic retry policies for transient failures
    /// - Request unit tracking and telemetry
    /// - Automatic count cache invalidation on success
    /// - Detailed exception data for programmatic error handling
    /// 
    /// On failure, throws <c>CosmoBaseException</c> with detailed result information accessible 
    /// via <c>ex.Data["BulkUpsertResult"]</c> for retry logic and error reporting.
    /// </remarks>
    Task BulkUpsertAsync(IEnumerable<T> items, string partitionKeyValue, int batchSize = 100, int maxConcurrency = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts multiple documents in batches with automatic audit field management (fails if any already exist).
    /// </summary>
    /// <param name="items">Documents to insert.</param>
    /// <param name="partitionKeyValue">Partition key value for all items.</param>
    /// <param name="batchSize">Max items per batch.</param>
    /// <param name="maxConcurrency">Max parallel requests.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <remarks>
    /// Each document in the batch receives complete audit field population:
    /// - <c>CreatedOnUtc</c>: Current UTC timestamp
    /// - <c>UpdatedOnUtc</c>: Current UTC timestamp  
    /// - <c>CreatedBy</c>: Current user from user context
    /// - <c>UpdatedBy</c>: Current user from user context
    /// - <c>Deleted</c>: false
    /// 
    /// Features:
    /// - Parallel processing with configurable concurrency limits
    /// - Comprehensive error handling with item-level failure details
    /// - Automatic retry policies for transient failures
    /// - Request unit tracking and telemetry
    /// - Automatic count cache invalidation on success
    /// - Detailed exception data for programmatic error handling
    /// 
    /// On failure, throws <c>CosmoBaseException</c> with detailed result information accessible 
    /// via <c>ex.Data["BulkInsertResult"]</c> for retry logic and error reporting.
    /// </remarks>
    Task BulkInsertAsync(IEnumerable<T> items, string partitionKeyValue, int batchSize = 100, int maxConcurrency = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a patch specification to a single document with automatic audit field management.
    /// </summary>
    /// <param name="id">The document's id.</param>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="patchSpecification">The patch operations to apply.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The patched document if successful; otherwise <c>null</c>.</returns>
    /// <remarks>
    /// Note: Patch operations do not automatically update audit fields as they use Cosmos DB's 
    /// server-side patch functionality. If audit field updates are required for patch operations,
    /// consider using <c>ReplaceItemAsync</c> instead or include audit field updates in your 
    /// patch specification.
    /// 
    /// The operation includes comprehensive validation and retry policies.
    /// </remarks>
    Task<T?> PatchItemAsync(string id, string partitionKey, PatchSpecification patchSpecification,
        CancellationToken cancellationToken = default);
}