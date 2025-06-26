using CosmoBase.Abstractions.Exceptions;
using CosmoBase.Abstractions.Filters;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Defines validation methods for Cosmos DB operations.
/// </summary>
/// <typeparam name="T">The document model type.</typeparam>
public interface ICosmosValidator<in T> where T : class, ICosmosDataModel, new()
{
    /// <summary>
    /// Validates the model configuration and partition key setup.
    /// </summary>
    /// <param name="partitionKeyProperty">The partition key property name.</param>
    /// <exception cref="CosmosConfigurationException">Thrown when configuration is invalid.</exception>
    void ValidateModelConfiguration(string partitionKeyProperty);

    /// <summary>
    /// Validates a document before any operation.
    /// </summary>
    /// <param name="item">The document to validate.</param>
    /// <param name="operation">The operation being performed.</param>
    /// <param name="partitionKeyProperty">The partition key property name.</param>
    /// <exception cref="ArgumentNullException">Thrown when item is null.</exception>
    /// <exception cref="ArgumentException">Thrown when document has invalid data.</exception>
    void ValidateDocument(T item, string operation, string partitionKeyProperty);

    /// <summary>
    /// Validates parameters for ID-based operations.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="operation">The operation name.</param>
    void ValidateIdAndPartitionKey(string id, string partitionKey, string operation);

    /// <summary>
    /// Validates a partition key value.
    /// </summary>
    /// <param name="partitionKey">The partition key to validate.</param>
    /// <param name="operation">The operation name.</param>
    void ValidatePartitionKey(string partitionKey, string operation);

    /// <summary>
    /// Validates parameters for paging operations.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="operation">The operation name.</param>
    void ValidatePagingParameters(int pageSize, string operation);

    /// <summary>
    /// Validates a collection of items for bulk operations.
    /// </summary>
    /// <param name="items">The items to validate.</param>
    /// <param name="partitionKeyValue">The expected partition key value.</param>
    /// <param name="partitionKeyProperty">The partition key property name.</param>
    /// <param name="operation">The operation name.</param>
    void ValidateBulkItems(IEnumerable<T> items, string partitionKeyValue, string partitionKeyProperty, string operation);

    /// <summary>
    /// Validates array property query parameters.
    /// </summary>
    /// <param name="arrayName">The array property name.</param>
    /// <param name="elementPropertyName">The element property name.</param>
    /// <param name="elementPropertyValue">The element property value.</param>
    void ValidateArrayPropertyQuery(string arrayName, string elementPropertyName, object elementPropertyValue);

    /// <summary>
    /// Validates property filter parameters.
    /// </summary>
    /// <param name="filters">The property filters.</param>
    void ValidatePropertyFilters(IEnumerable<PropertyFilter> filters);

    /// <summary>
    /// Validates cache expiry parameters.
    /// </summary>
    /// <param name="cacheExpiryMinutes">The cache expiry in minutes.</param>
    void ValidateCacheExpiry(int cacheExpiryMinutes);

    /// <summary>
    /// Validates bulk operation parameters.
    /// </summary>
    /// <param name="batchSize">The batch size.</param>
    /// <param name="maxConcurrency">The max concurrency.</param>
    void ValidateBulkOperationParameters(int batchSize, int maxConcurrency);
}