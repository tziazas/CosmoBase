using CosmoBase.Abstractions.Filters;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// 
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IDataReadService<T>
    where T : class
{
    //Task<T?> GetByIdAsync(string id);
    Task<T?> GetByIdAsync(string id, string partitionKey);
    IAsyncEnumerable<T> GetByQuery(CancellationToken cancellationToken, string query);
    Task<IList<T>> GetAllByPropertyAsync(
        CancellationToken cancellationToken,
        string propertyName,
        string propertyValue
    );
    Task<IList<T>> GetAllDistinctInListByPropertyAsync(
        CancellationToken cancellationToken,
        string propertyName,
        List<string> propertyValueList
    );
    Task<IList<T>> GetAllByPropertyComparisonAsync(
        CancellationToken cancellationToken,
        List<PropertyFilter> propertyFilters
    );
    Task<IList<T>> GetAllByArrayPropertyAsync(
        CancellationToken cancellationToken,
        string arrayName,
        string arrayPropertyName,
        string propertyValue
    );

    IAsyncEnumerable<T> GetAllAsyncEnumerable(
        CancellationToken cancellationToken,
        string partitionKey
    );
    IAsyncEnumerable<T> GetAllAsyncEnumerable(
        CancellationToken cancellationToken,
        int limit,
        int offset,
        int count
    );
}