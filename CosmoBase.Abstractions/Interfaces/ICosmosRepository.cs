using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Filters;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// The repository allows direct connection to the database and should not be
/// used directly in an API or a Client.
///
/// Feel free to add and/or remove methods that can be used across all repositories.
///
/// If a method is unique to a specific Type (class), please use a Service and extend
/// the capabilities that way.
///
/// When we are modifying an entire document, the ICosmosDataModel is used in order
/// to ensure we are not accidentally passing an incorrect Type to the database and
/// potentially create problematic data.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ICosmosRepository<T>
    where T : class
{
    IQueryable<T> Queryable { get; }
    Task<T?> GetByIdAsync(string id);
    Task<T?> GetByIdAsync(string id, string partitionKey);
    Task DeleteAsync(string id, DeleteOptions deleteOptions);
    Task DeleteAsync(string id, string partitionKey, DeleteOptions deleteOptions);
    Task<T> AddAsync(ICosmosDataModel document);
    Task<T> UpdateAsync(ICosmosDataModel document);
    Task<T> CreateAsync(ICosmosDataModel document);
    Task<List<T>> GetAllByPropertyAsync(string propertyName, string propertyValue);
    Task<IList<T>> GetAllDistinctInListByPropertyAsync(
        CancellationToken cancellationToken,
        string propertyName,
        List<string> propertyValueList
    );
    Task<List<T>> GetAllByArrayPropertyAsync(
        string arrayName,
        string arrayPropertyName,
        string propertyValue
    );
    Task<List<T>> GetAllByPropertyComparisonAsync(List<PropertyFilter> propertyFilters);
    IAsyncEnumerable<T> GetAll(CancellationToken cancellationToken);
    IAsyncEnumerable<T> GetAll(CancellationToken cancellationToken, string partitionKey);
    IAsyncEnumerable<T> GetAll(
        CancellationToken cancellationToken,
        int limit,
        int offset,
        int count
    );
    IAsyncEnumerable<T> GetByQuery(CancellationToken cancellationToken, string query);
}