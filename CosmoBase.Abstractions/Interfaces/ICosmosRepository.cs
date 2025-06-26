using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Filters;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Provides low-level CRUD and query capabilities against a Cosmos DB container.
/// This repository should not be injected directly into API or client code—
/// use a higher-level service to encapsulate business logic and DTO mapping.
/// </summary>
/// <typeparam name="T">
/// The document model type stored in Cosmos (must implement <see cref="ICosmosDataModel"/>).
/// </typeparam>
public interface ICosmosRepository<T>
    where T : class, ICosmosDataModel, new()
{
    /// <summary>
    /// Exposes an <see cref="IQueryable{T}"/> over the container for custom LINQ queries.
    /// </summary>
    IQueryable<T> Queryable { get; }

    /// <summary>
    /// Retrieves a single document by its id and partition key.
    /// </summary>
    /// <param name="id">The document’s unique identifier.</param>
    /// <param name="partitionKey">The value of the partition key for this document.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The document if found; otherwise <c>null</c>.</returns>
    Task<T?> GetItemAsync(string id, string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new document. Throws if a document with the same id already exists.
    /// </summary>
    /// <param name="item">The document to create.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created document.</returns>
    Task<T> CreateItemAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing document. Throws if the document does not exist.
    /// </summary>
    /// <param name="item">The document to replace.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The replaced document.</returns>
    Task<T> ReplaceItemAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces a document (upsert semantics).
    /// </summary>
    /// <param name="item">The document to upsert.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The upserted document.</returns>
    Task<T> UpsertItemAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document by id and partition key.
    /// </summary>
    /// <param name="id">The document’s id.</param>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="deleteOptions">
    /// Options controlling soft-vs-hard delete behavior, TTL, etc.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
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
    /// </summary>
    /// <param name="partitionKey">Partition key value.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The document count.</returns>
    Task<int> GetCountAsync(string partitionKey, CancellationToken cancellationToken = default);

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
    /// Finds documents where an array property contains an element with the specified property value.
    /// </summary>
    /// <param name="arrayName">The name of the array property.</param>
    /// <param name="elementPropertyName">The name of the property within each array element.</param>
    /// <param name="elementPropertyValue">The value to match in the array elements.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of matching documents.</returns>
    Task<List<T>> GetAllByArrayPropertyAsync(string arrayName, string elementPropertyName, object elementPropertyValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all documents that satisfy a set of property filters.
    /// </summary>
    /// <param name="propertyFilters">The list of property filters to apply.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of matching documents.</returns>
    Task<List<T>> GetAllByPropertyComparisonAsync(IEnumerable<PropertyFilter> propertyFilters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts multiple documents in batches.
    /// </summary>
    /// <param name="items">Documents to upsert.</param>
    /// <param name="partitionKeyValue">Partition key value for all items.</param>
    /// <param name="batchSize">Max items per batch.</param>
    /// <param name="maxConcurrency">Max parallel requests.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task BulkUpsertAsync(IEnumerable<T> items, string partitionKeyValue, int batchSize = 100, int maxConcurrency = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts multiple documents in batches (fails if any already exist).
    /// </summary>
    /// <param name="items">Documents to insert.</param>
    /// <param name="partitionKeyValue">Partition key value for all items.</param>
    /// <param name="batchSize">Max items per batch.</param>
    /// <param name="maxConcurrency">Max parallel requests.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task BulkInsertAsync(IEnumerable<T> items, string partitionKeyValue, int batchSize = 100, int maxConcurrency = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a patch specification to a single document.
    /// </summary>
    /// <param name="id">The document’s id.</param>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="patchSpecification">The patch operations to apply.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The patched document if successful; otherwise <c>null</c>.</returns>
    Task<T?> PatchItemAsync(string id, string partitionKey, PatchSpecification patchSpecification,
        CancellationToken cancellationToken = default);
}