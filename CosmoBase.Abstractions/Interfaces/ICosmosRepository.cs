using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Filters;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Provides low-level CRUD and query capabilities against a Cosmos DB container.
/// This repository should not be injected directly into API or client code—use
/// a higher-level service to encapsulate business logic and DTO mapping.
/// </summary>
/// <typeparam name="T">
/// The document model type stored in Cosmos (must implement <see cref="ICosmosDataModel"/>).
/// </typeparam>
public interface ICosmosRepository<T>
    where T : class
{
    /// <summary>
    /// Exposes an <see cref="IQueryable{T}"/> over the container for custom LINQ queries.
    /// </summary>
    IQueryable<T> Queryable { get; }

    /// <summary>
    /// Retrieves a single document by its id.
    /// </summary>
    /// <param name="id">The document’s unique identifier.</param>
    /// <returns>The document if found; otherwise <c>null</c>.</returns>
    Task<T?> GetByIdAsync(string id);

    /// <summary>
    /// Retrieves a single document by its id and partition key.
    /// </summary>
    /// <param name="id">The document’s unique identifier.</param>
    /// <param name="partitionKey">The value of the partition key for this document.</param>
    /// <returns>The document if found; otherwise <c>null</c>.</returns>
    Task<T?> GetByIdAsync(string id, string partitionKey);

    /// <summary>
    /// Deletes a document by id using the specified <paramref name="deleteOptions"/>.
    /// </summary>
    /// <param name="id">The id of the document to delete.</param>
    /// <param name="deleteOptions">
    /// Options controlling soft-vs-hard delete behavior, TTL, etc.
    /// </param>
    Task DeleteAsync(string id, DeleteOptions deleteOptions);

    /// <summary>
    /// Deletes a document by id and partition key using the specified <paramref name="deleteOptions"/>.
    /// </summary>
    /// <param name="id">The id of the document to delete.</param>
    /// <param name="partitionKey">The partition key value of the document.</param>
    /// <param name="deleteOptions">
    /// Options controlling soft-vs-hard delete behavior, TTL, etc.
    /// </param>
    Task DeleteAsync(string id, string partitionKey, DeleteOptions deleteOptions);

    /// <summary>
    /// Adds a new document (or performs an upsert) based on the <paramref name="document"/>.
    /// </summary>
    /// <param name="document">
    /// The document to add; must implement <see cref="ICosmosDataModel"/>.
    /// </param>
    /// <returns>The created or updated document.</returns>
    Task<T> AddAsync(ICosmosDataModel document);

    /// <summary>
    /// Updates an existing document in the container.
    /// </summary>
    /// <param name="document">
    /// The document to update; must implement <see cref="ICosmosDataModel"/>.
    /// </param>
    /// <returns>The updated document.</returns>
    Task<T> UpdateAsync(ICosmosDataModel document);

    /// <summary>
    /// Creates a new document. Throws if a document with the same id already exists.
    /// </summary>
    /// <param name="document">
    /// The document to create; must implement <see cref="ICosmosDataModel"/>.
    /// </param>
    /// <returns>The created document.</returns>
    Task<T> CreateAsync(ICosmosDataModel document);

    /// <summary>
    /// Retrieves all documents where a single property matches <paramref name="propertyValue"/>.
    /// </summary>
    /// <param name="propertyName">The document property to filter on.</param>
    /// <param name="propertyValue">The value to match.</param>
    /// <returns>A list of matching documents.</returns>
    Task<List<T>> GetAllByPropertyAsync(string propertyName, string propertyValue);

    /// <summary>
    /// Retrieves all documents where the given <paramref name="propertyName"/> is in the
    /// provided <paramref name="propertyValueList"/>, eliminating duplicates.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <param name="propertyName">The document property to filter on.</param>
    /// <param name="propertyValueList">The list of values to match.</param>
    /// <returns>A list of matching documents, distinct by the specified property.</returns>
    Task<IList<T>> GetAllDistinctInListByPropertyAsync(
        CancellationToken cancellationToken,
        string propertyName,
        List<string> propertyValueList
    );

    /// <summary>
    /// Retrieves all documents where an array property contains <paramref name="propertyValue"/>.
    /// </summary>
    /// <param name="arrayName">The name of the array property.</param>
    /// <param name="arrayPropertyName">The name of the property within each array element.</param>
    /// <param name="propertyValue">The value to match in the array elements.</param>
    /// <returns>A list of matching documents.</returns>
    Task<List<T>> GetAllByArrayPropertyAsync(
        string arrayName,
        string arrayPropertyName,
        string propertyValue
    );

    /// <summary>
    /// Retrieves all documents that satisfy a set of <paramref name="propertyFilters"/>
    /// (property/operator/value triplets).
    /// </summary>
    /// <param name="propertyFilters">The list of property filters to apply.</param>
    /// <returns>A list of matching documents.</returns>
    Task<List<T>> GetAllByPropertyComparisonAsync(List<PropertyFilter> propertyFilters);

    /// <summary>
    /// Streams all non-deleted documents asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <returns>An async stream of documents.</returns>
    IAsyncEnumerable<T> GetAll(CancellationToken cancellationToken);

    /// <summary>
    /// Streams all non-deleted documents within a specific partition.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <param name="partitionKey">The partition key to scope the query.</param>
    /// <returns>An async stream of documents.</returns>
    IAsyncEnumerable<T> GetAll(CancellationToken cancellationToken, string partitionKey);

    /// <summary>
    /// Streams a subset of documents by applying <paramref name="offset"/>,
    /// <paramref name="limit"/> (page size), and a total <paramref name="count"/> cap.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <param name="limit">Max items to request per server-side page.</param>
    /// <param name="offset">Number of items to skip before streaming.</param>
    /// <param name="count">Total number of items to yield.</param>
    /// <returns>An async stream of documents after offset/limit/count.</returns>
    IAsyncEnumerable<T> GetAll(
        CancellationToken cancellationToken,
        int limit,
        int offset,
        int count
    );

    /// <summary>
    /// Streams results of an ad-hoc SQL query string.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <param name="query">A valid Cosmos DB SQL query.</param>
    /// <returns>An async stream of documents matching the query.</returns>
    IAsyncEnumerable<T> GetByQuery(CancellationToken cancellationToken, string query);
}