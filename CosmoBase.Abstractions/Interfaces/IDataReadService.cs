using CosmoBase.Abstractions.Filters;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Provides read‐only data operations for <typeparamref name="T"/> DTOs.
/// Use this service to retrieve, query, and filter documents without exposing
/// the underlying repository or database details.
/// </summary>
/// <typeparam name="T">
/// The DTO type returned by this service. Must be a reference type.
/// </typeparam>
public interface IDataReadService<T>
    where T : class
{
    /// <summary>
    /// Retrieves a single document by its id and partition key.
    /// </summary>
    /// <param name="id">The unique identifier of the document.</param>
    /// <param name="partitionKey">
    /// The partition key value of the document (for partitioned containers).
    /// </param>
    /// <returns>
    /// The matching document as <typeparamref name="T"/>, or <c>null</c> if not found.
    /// </returns>
    Task<T?> GetByIdAsync(string id, string partitionKey);

    /// <summary>
    /// Streams the results of an arbitrary SQL query against the container.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to observe while streaming results for cooperative cancellation.
    /// </param>
    /// <param name="query">A valid Cosmos DB SQL query string.</param>
    /// <returns>
    /// An async‐stream of <typeparamref name="T"/> objects matching the query.
    /// </returns>
    IAsyncEnumerable<T> GetByQuery(
        CancellationToken cancellationToken,
        string query
    );

    /// <summary>
    /// Retrieves all documents where a simple property equals a given value.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <param name="propertyName">The name of the property to filter on.</param>
    /// <param name="propertyValue">The value to match.</param>
    /// <returns>
    /// A list of <typeparamref name="T"/> matching the property equality filter.
    /// </returns>
    Task<IList<T>> GetAllByPropertyAsync(
        CancellationToken cancellationToken,
        string propertyName,
        string propertyValue
    );

    /// <summary>
    /// Retrieves all documents where the specified property’s value is in a given list,
    /// returning each document only once.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <param name="propertyName">The name of the property to filter on.</param>
    /// <param name="propertyValueList">
    /// A list of values; documents whose property matches any value will be returned.
    /// </param>
    /// <returns>
    /// A distinct list of <typeparamref name="T"/> matching the “IN” filter.
    /// </returns>
    Task<IList<T>> GetAllDistinctInListByPropertyAsync(
        CancellationToken cancellationToken,
        string propertyName,
        List<string> propertyValueList
    );

    /// <summary>
    /// Retrieves all documents that satisfy a collection of property‐comparison filters.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <param name="propertyFilters">
    /// A list of <see cref="PropertyFilter"/> specifying property, operator, and value.
    /// </param>
    /// <returns>
    /// A list of <typeparamref name="T"/> matching all provided filters.
    /// </returns>
    Task<IList<T>> GetAllByPropertyComparisonAsync(
        CancellationToken cancellationToken,
        List<PropertyFilter> propertyFilters
    );

    /// <summary>
    /// Retrieves all documents where a nested array property contains a given value.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <param name="arrayName">The top‐level array property name.</param>
    /// <param name="arrayPropertyName">
    /// The property name within each array element to match against.
    /// </param>
    /// <param name="propertyValue">The value to search for inside the array elements.</param>
    /// <returns>
    /// A list of <typeparamref name="T"/> containing the specified value in the nested array.
    /// </returns>
    Task<IList<T>> GetAllByArrayPropertyAsync(
        CancellationToken cancellationToken,
        string arrayName,
        string arrayPropertyName,
        string propertyValue
    );

    /// <summary>
    /// Streams all non‐deleted documents from a specific partition.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <param name="partitionKey">The partition key to scope the query.</param>
    /// <returns>An async‐stream of <typeparamref name="T"/> in the specified partition.</returns>
    IAsyncEnumerable<T> GetAllAsyncEnumerable(
        CancellationToken cancellationToken,
        string partitionKey
    );

    /// <summary>
    /// Streams a subset of documents by applying an initial <paramref name="offset"/>,
    /// using pages of <paramref name="limit"/>, and yielding up to <paramref name="count"/>.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the async stream.</param>
    /// <param name="limit">Maximum items to request per server‐side page.</param>
    /// <param name="offset">Number of items to skip before yielding results.</param>
    /// <param name="count">Total number of items to yield.</param>
    /// <returns>
    /// An async‐stream of <typeparamref name="T"/> after applying offset/limit/count.
    /// </returns>
    IAsyncEnumerable<T> GetAllAsyncEnumerable(
        CancellationToken cancellationToken,
        int limit,
        int offset,
        int count
    );
}