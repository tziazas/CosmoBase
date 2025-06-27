using CosmoBase.Abstractions.Exceptions;
using CosmoBase.Abstractions.Filters;
using CosmoBase.Abstractions.Interfaces;

namespace CosmoBase.Core.Validation;

/// <summary>
/// Default implementation of Cosmos DB validation logic.
/// </summary>
/// <typeparam name="T">The document model type.</typeparam>
public class CosmosValidator<T> : ICosmosValidator<T> where T : class, ICosmosDataModel, new()
{
    /// <inheritdoc/>
    public void ValidateModelConfiguration(string partitionKeyProperty)
    {
        var modelType = typeof(T);
        
        // 1. Validate partition key property exists
        var partitionKeyProp = modelType.GetProperty(partitionKeyProperty);
        if (partitionKeyProp == null)
        {
            var availableProps = string.Join(", ", modelType.GetProperties().Select(p => p.Name));
            var message = $"Partition key property '{partitionKeyProperty}' not found on type '{modelType.Name}'. " +
                         $"Available properties: {availableProps}";
            throw new CosmosConfigurationException(message);
        }

        // 2. Validate partition key property type is supported
        if (!CosmosValidationConstants.SupportedPartitionKeyTypes.Contains(partitionKeyProp.PropertyType))
        {
            var supportedTypeNames = CosmosValidationConstants.SupportedPartitionKeyTypes.Select(t => t.Name);
            var message = $"Partition key property '{partitionKeyProperty}' on type '{modelType.Name}' " +
                         $"has unsupported type '{partitionKeyProp.PropertyType.Name}'. " +
                         $"Supported types: {string.Join(", ", supportedTypeNames)}";
            throw new CosmosConfigurationException(message);
        }

        // 3. Validate partition key property is readable
        if (!partitionKeyProp.CanRead)
        {
            var message = $"Partition key property '{partitionKeyProperty}' on type '{modelType.Name}' is not readable";
            throw new CosmosConfigurationException(message);
        }

        // 4. Validate required ICosmosDataModel properties exist
        ValidateRequiredProperties(modelType);
    }

    /// <inheritdoc/>
    public void ValidateDocument(T item, string operation, string partitionKeyProperty)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item), $"Document cannot be null for {operation}");

        var errors = new List<string>();

        // 1. Validate ID
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            errors.Add("Document ID cannot be null or empty");
        }
        else if (item.Id.Length > CosmosValidationConstants.MaxDocumentIdLength)
        {
            errors.Add($"Document ID cannot exceed {CosmosValidationConstants.MaxDocumentIdLength} characters");
        }
        else if (ContainsInvalidIdCharacters(item.Id))
        {
            errors.Add("Document ID contains invalid characters (/, \\, ?, #)");
        }

        // 2. Validate partition key value
        try
        {
            var partitionKeyValue = GetPartitionKeyValue(item, partitionKeyProperty);
            if (string.IsNullOrEmpty(partitionKeyValue))
            {
                errors.Add($"Partition key property '{partitionKeyProperty}' cannot be null or empty");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to read partition key value: {ex.Message}");
        }

        // 3. Validate audit fields for updates (not creates)
        if (operation != "Create")
        {
            if (!item.CreatedOnUtc.HasValue)
            {
                errors.Add("CreatedOnUtc must have a value for existing documents");
            }
        }

        // 4. Check for obviously invalid data
        if (item.CreatedOnUtc.HasValue && item.UpdatedOnUtc.HasValue && 
            item.CreatedOnUtc.Value > item.UpdatedOnUtc.Value)
        {
            errors.Add("CreatedOnUtc cannot be after UpdatedOnUtc");
        }

        if (errors.Any())
        {
            var message = $"Document validation failed for {operation}: {string.Join("; ", errors)}";
            throw new ArgumentException(message, nameof(item));
        }
    }

    /// <inheritdoc/>
    public void ValidateIdAndPartitionKey(string id, string partitionKey, string operation)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException($"Document ID cannot be null or empty for {operation}", nameof(id));
        
        if (id.Length > CosmosValidationConstants.MaxDocumentIdLength)
            throw new ArgumentException($"Document ID cannot exceed {CosmosValidationConstants.MaxDocumentIdLength} characters for {operation}", nameof(id));
        
        if (ContainsInvalidIdCharacters(id))
            throw new ArgumentException($"Document ID contains invalid characters (/, \\, ?, #) for {operation}", nameof(id));

        ValidatePartitionKey(partitionKey, operation);
    }

    /// <inheritdoc/>
    public void ValidatePartitionKey(string partitionKey, string operation)
    {
        if (string.IsNullOrWhiteSpace(partitionKey))
            throw new ArgumentException($"Partition key cannot be null or empty for {operation}", nameof(partitionKey));
    }

    /// <inheritdoc/>
    public void ValidatePagingParameters(int pageSize, string operation)
    {
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, 
                $"Page size must be positive for {operation}");
        
        if (pageSize > CosmosValidationConstants.MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, 
                $"Page size cannot exceed {CosmosValidationConstants.MaxPageSize} for {operation} (Cosmos DB limitation)");
    }

    /// <inheritdoc/>
    public void ValidateBulkItems(IEnumerable<T> items, string partitionKeyValue, string partitionKeyProperty, string operation)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items), $"Items collection cannot be null for {operation}");

        ValidatePartitionKey(partitionKeyValue, operation);

        var itemList = items.ToList();
        if (!itemList.Any())
        {
            return; // Empty collection is valid
        }

        // Validate each item and check partition key consistency
        var errors = new List<string>();
        for (var i = 0; i < itemList.Count; i++)
        {
            var item = itemList[i];
            try
            {
                ValidateDocument(item, operation, partitionKeyProperty);
                
                // Check partition key consistency
                var itemPartitionKey = GetPartitionKeyValue(item, partitionKeyProperty);
                if (!string.Equals(itemPartitionKey, partitionKeyValue, StringComparison.Ordinal))
                {
                    errors.Add($"Item[{i}] (ID: {item.Id}): partition key mismatch - expected '{partitionKeyValue}', got '{itemPartitionKey}'");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Item[{i}] (ID: {item.Id}): {ex.Message}");
            }
        }

        if (errors.Any())
        {
            var message = $"Bulk {operation} validation failed:\n{string.Join("\n", errors)}";
            throw new ArgumentException(message, nameof(items));
        }
    }

    /// <inheritdoc/>
    public void ValidateArrayPropertyQuery(string arrayName, string elementPropertyName, object elementPropertyValue)
    {
        if (string.IsNullOrWhiteSpace(arrayName))
            throw new ArgumentException("Array name cannot be null or empty", nameof(arrayName));
        
        if (string.IsNullOrWhiteSpace(elementPropertyName))
            throw new ArgumentException("Element property name cannot be null or empty", nameof(elementPropertyName));
        
        if (elementPropertyValue == null)
            throw new ArgumentNullException(nameof(elementPropertyValue), "Element property value cannot be null");
    }

    /// <inheritdoc/>
    public void ValidatePropertyFilters(IEnumerable<PropertyFilter> filters)
    {
        if (filters == null)
            throw new ArgumentNullException(nameof(filters), "Property filters cannot be null");
        
        var filterList = filters.ToList();
        
        // Validate each filter
        for (var i = 0; i < filterList.Count; i++)
        {
            var filter = filterList[i];
            if (filter == null)
                throw new ArgumentException($"Filter at index {i} cannot be null", nameof(filters));
            
            if (string.IsNullOrWhiteSpace(filter.PropertyName))
                throw new ArgumentException($"Filter at index {i} has null or empty PropertyName", nameof(filters));
        }
    }

    /// <inheritdoc/>
    public void ValidateCacheExpiry(int cacheExpiryMinutes)
    {
        if (cacheExpiryMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(cacheExpiryMinutes),
                "Cache expiry minutes cannot be negative");
    }

    /// <inheritdoc/>
    public void ValidateBulkOperationParameters(int batchSize, int maxConcurrency)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive");
        
        if (maxConcurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Max concurrency must be positive");
        
        if (batchSize > CosmosValidationConstants.MaxBatchSize)
            throw new ArgumentOutOfRangeException(nameof(batchSize), 
                $"Batch size should not exceed {CosmosValidationConstants.MaxBatchSize} for optimal Cosmos DB performance");
        
        if (maxConcurrency > CosmosValidationConstants.MaxConcurrency)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), 
                $"Max concurrency should not exceed {CosmosValidationConstants.MaxConcurrency} to avoid overwhelming Cosmos DB");
    }

    #region Private Helper Methods

    /// <summary>
    /// Validates that required ICosmosDataModel properties are properly implemented.
    /// </summary>
    private static void ValidateRequiredProperties(Type modelType)
    {
        var requiredProperties = new[]
        {
            nameof(ICosmosDataModel.Id),
            nameof(ICosmosDataModel.CreatedOnUtc),
            nameof(ICosmosDataModel.UpdatedOnUtc),
            nameof(ICosmosDataModel.CreatedBy),
            nameof(ICosmosDataModel.UpdatedBy),
            nameof(ICosmosDataModel.Deleted)
        };

        var missingProperties = new List<string>();
        
        foreach (var propName in requiredProperties)
        {
            var prop = modelType.GetProperty(propName);
            if (prop == null)
            {
                missingProperties.Add(propName);
            }
            else if (!prop.CanRead || !prop.CanWrite)
            {
                missingProperties.Add($"{propName} (not readable/writable)");
            }
        }

        if (missingProperties.Any())
        {
            var message = $"Type '{modelType.Name}' is missing required ICosmosDataModel properties: " +
                         string.Join(", ", missingProperties);
            throw new CosmosConfigurationException(message);
        }
    }

    /// <summary>
    /// Checks if an ID contains characters that are invalid in Cosmos DB.
    /// </summary>
    private static bool ContainsInvalidIdCharacters(string id)
    {
        return id.IndexOfAny(CosmosValidationConstants.InvalidIdCharacters) >= 0;
    }

    /// <summary>
    /// Reads the partition key value from an item using reflection.
    /// </summary>
    private static string GetPartitionKeyValue(T item, string partitionKeyProperty)
    {
        var prop = typeof(T).GetProperty(partitionKeyProperty)
                   ?? throw new InvalidOperationException(
                       $"Partition-key property '{partitionKeyProperty}' not found on type '{typeof(T).Name}'");
        var raw = prop.GetValue(item)
                  ?? throw new InvalidOperationException(
                      $"Partition-key value for '{partitionKeyProperty}' on '{typeof(T).Name}' was null");
        return raw.ToString()!;
    }

    #endregion
}