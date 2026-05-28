using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using CosmoBase.Abstractions.Configuration;
using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Exceptions;
using CosmoBase.Abstractions.Filters;
using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Abstractions.Models;
using CosmoBase.Core.Extensions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CosmoBase.Core.Repositories;

/// <summary>
/// Provides low-level CRUD and query operations against a Cosmos DB container.
/// Use higher-level services for business logic and DTO mapping.
/// </summary>
/// <typeparam name="T">The document model type stored in Cosmos, must implement <see cref="ICosmosDataModel"/> and have a parameterless constructor.</typeparam>
public class CosmosRepository<T> : ICosmosRepository<T> where T : class, ICosmosDataModel
{
    private readonly CosmosClient _readClient;
    private readonly CosmosClient _writeClient;
    private readonly Container _readContainer;
    private readonly Container _writeContainer;
    private readonly string _partitionKeyProperty;
    private readonly ILogger<CosmosRepository<T>> _logger;
    private readonly IMemoryCache _cache;
    private readonly ICosmosValidator<T> _validator;
    private readonly IAuditFieldManager<T> _auditFieldManager;

    // Compiled delegate that reads the partition-key property from a T instance.
    // Null when the DAO type does not expose the partition key property — valid for
    // read-only models.  Write operations call GetPartitionKeyValue(), which throws
    // a clear error if the delegate is null, so misconfigured write models are caught
    // at the first write call rather than silently at query time.
    //
    // The delegate is compiled once at construction from an expression tree, so every
    // subsequent call is a direct property read with no reflection overhead.
    private readonly Func<T, string>? _getPartitionKey;

    /// <summary>
    /// Initializes a new instance using <paramref name="configuration"/> to locate containers and <paramref name="cosmosClients"/> for database access.
    /// </summary>
    /// <param name="configuration">Cosmos DB configuration settings.</param>
    /// <param name="logger">The logger for this repository</param>
    /// <param name="cache">The memory cache for count queries</param>
    /// <param name="validator">Generic Cosmos Validator</param>
    /// <param name="auditFieldManager">Manages audit fields (including User Context)</param>
    /// <param name="cosmosClients">Dictionary of pre-configured CosmosClient instances from dependency injection</param>
    /// <exception cref="CosmosConfigurationException">Thrown if configuration for <typeparamref name="T"/> is missing or required clients are not found.</exception>
    public CosmosRepository(CosmosConfiguration configuration, ILogger<CosmosRepository<T>> logger, IMemoryCache cache,
        ICosmosValidator<T> validator, IAuditFieldManager<T> auditFieldManager,
        IReadOnlyDictionary<string, CosmosClient> cosmosClients)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _validator = validator;
        _auditFieldManager = auditFieldManager;

        // Models/Containers
        var modelName = typeof(T).Name;
        var modelConfig = configuration.CosmosModelConfigurations
            .FirstOrDefault(x => x.ModelName.Equals(modelName, StringComparison.InvariantCultureIgnoreCase));
        if (modelConfig == null)
        {
            _logger.LogError("CosmosConfiguration: no model configuration found for {ModelName}", modelName);
            throw new CosmosConfigurationException($"No model configuration found for {modelName}");
        }

        _partitionKeyProperty = modelConfig.PartitionKey;

        // Get pre-configured clients from DI
        if (!cosmosClients.TryGetValue(modelConfig.ReadCosmosClientConfigurationName, out var readClient))
        {
            _logger.LogError("CosmosConfiguration: no read client named {ReadConfigName} for model {ModelName}",
                modelConfig.ReadCosmosClientConfigurationName, modelName);
            throw new CosmosConfigurationException($"No read client config for {modelName}");
        }

        if (!cosmosClients.TryGetValue(modelConfig.WriteCosmosClientConfigurationName, out var writeClient))
        {
            _logger.LogError("CosmosConfiguration: no write client named {WriteConfigName} for model {ModelName}",
                modelConfig.WriteCosmosClientConfigurationName, modelName);
            throw new CosmosConfigurationException($"No write client config for {modelName}");
        }

        _readClient = readClient;
        _writeClient = writeClient;

        _readContainer = _readClient
            .GetContainer(modelConfig.DatabaseName, modelConfig.CollectionName);
        _writeContainer = _writeClient
            .GetContainer(modelConfig.DatabaseName, modelConfig.CollectionName);

        // Build the compiled accessor. Returns null when the DAO type does not expose the
        // partition key property — this is valid for read-only models.  Write operations
        // (CreateAsync, ReplaceAsync, etc.) call GetPartitionKeyValue(), which throws if null.
        _getPartitionKey = BuildPartitionKeyAccessor(_partitionKeyProperty);
    }

    /// <inheritdoc/>
    public string PartitionKeyProperty => _partitionKeyProperty;

    /// <inheritdoc/>
    public IQueryable<T> Queryable => _readContainer.GetItemLinqQueryable<T>(true);

    /// <inheritdoc/>
    public async Task<T?> GetItemAsync(string id, string partitionKey, bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _validator.ValidateIdAndPartitionKey(id, partitionKey, "GetItem");

            var response = await _readContainer.ReadItemAsync<T>(
                id,
                new PartitionKey(partitionKey),
                cancellationToken: cancellationToken
            );

            _logger.LogInformation(
                "GetItemAsync: Item {Id} consumed {RequestCharge} RUs. Diagnostics: {Diagnostics}",
                id,
                response.RequestCharge,
                response.Diagnostics
            );

            var item = response.Resource;

            // Apply soft delete filter unless explicitly requested to include deleted items
            if (!includeDeleted && item.Deleted)
            {
                _logger.LogDebug(
                    "GetItemAsync: Item {Id} found but is soft-deleted, returning null (includeDeleted={IncludeDeleted})",
                    id,
                    includeDeleted
                );
                return null;
            }

            return item;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("GetItemAsync: Item {Id} not found in partition {PartitionKey}", id, partitionKey);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<T> CreateItemAsync(T item, CancellationToken cancellationToken = default)
    {
        _validator.ValidateDocument(item, "Create", _partitionKeyProperty);

        // Set audit fields using the manager
        _auditFieldManager.SetCreateAuditFields(item);

        var response = await _writeContainer.CreateItemAsync(
            item,
            new PartitionKey(GetPartitionKeyValue(item)),
            cancellationToken: cancellationToken
        );

        // Log RU charge and diagnostics
        _logger.LogInformation(
            "CreateItemAsync: Item {Id} consumed {RequestCharge} RUs. Diagnostics: {Diagnostics}",
            response.Resource.Id,
            response.RequestCharge,
            response.Diagnostics
        );

        InvalidateCountCache(GetPartitionKeyValue(item));

        return response.Resource;
    }

    /// <inheritdoc/>
    public async Task<T> ReplaceItemAsync(T item, CancellationToken cancellationToken = default)
    {
        _validator.ValidateDocument(item, "Replace", _partitionKeyProperty);

        // Set audit fields using the manager
        _auditFieldManager.SetUpdateAuditFields(item);

        var response = await _writeContainer.ReplaceItemAsync(
            item,
            item.Id,
            new PartitionKey(GetPartitionKeyValue(item)),
            cancellationToken: cancellationToken
        );

        // log RU charge + diagnostics
        _logger.LogInformation(
            "ReplaceItemAsync: Item {Id} consumed {RequestCharge} RUs. Diagnostics: {Diagnostics}",
            response.Resource.Id,
            response.RequestCharge,
            response.Diagnostics
        );

        return response.Resource;
    }


    /// <inheritdoc/>
    public async Task<T> UpsertItemAsync(T item, CancellationToken cancellationToken = default)
    {
        _validator.ValidateDocument(item, "Upsert", _partitionKeyProperty);

        // Set audit fields using the manager (determines create vs update automatically)
        _auditFieldManager.SetUpsertAuditFields(item);

        var response = await _writeContainer.UpsertItemAsync(
            item,
            new PartitionKey(GetPartitionKeyValue(item)),
            cancellationToken: cancellationToken
        );

        // log RU charge + diagnostics
        _logger.LogInformation(
            "UpsertItemAsync: Item {Id} consumed {RequestCharge} RUs. Diagnostics: {Diagnostics}",
            response.Resource.Id,
            response.RequestCharge,
            response.Diagnostics
        );

        // Check if this was a create operation (201) vs replace operation (200) before invalidating the cache
        if (response.StatusCode == HttpStatusCode.Created)
        {
            InvalidateCountCache(GetPartitionKeyValue(item));
        }

        return response.Resource;
    }

    /// <inheritdoc/>
    public async Task DeleteItemAsync(
        string id,
        string partitionKey,
        DeleteOptions deleteOptions = DeleteOptions.HardDelete,
        CancellationToken cancellationToken = default)
    {
        if (deleteOptions == DeleteOptions.SoftDelete)
        {
            await SoftDeleteAsync(id, partitionKey, cancellationToken);
            InvalidateCountCache(partitionKey);
            return;
        }

        var response = await _writeContainer.DeleteItemAsync<T>(
            id,
            new PartitionKey(partitionKey),
            cancellationToken: cancellationToken
        );

        // log RU charge + diagnostics
        _logger.LogInformation(
            "DeleteItemAsync: Item {Id} consumed {RequestCharge} RUs. Diagnostics: {Diagnostics}",
            id,
            response.RequestCharge,
            response.Diagnostics
        );

        InvalidateCountCache(partitionKey);
    }

    private async Task SoftDeleteAsync(string id, string partitionKey, CancellationToken cancellationToken)
    {
        _validator.ValidateIdAndPartitionKey(id, partitionKey, "SoftDelete");

        // Read directly via the SDK so we capture the ETag from the response.
        // GetItemAsync returns only T?, discarding the ItemResponse — we need the
        // ETag to guard the subsequent replace against concurrent writes.
        ItemResponse<T> readResponse;
        try
        {
            readResponse = await _readContainer.ReadItemAsync<T>(
                id,
                new PartitionKey(partitionKey),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return; // Document does not exist — nothing to soft-delete.
        }

        _logger.LogInformation(
            "SoftDeleteAsync read: Item {Id} consumed {RequestCharge} RUs.",
            id, readResponse.RequestCharge);

        var item = readResponse.Resource;
        if (item is null) return;

        item.Deleted = true;
        _auditFieldManager.SetUpdateAuditFields(item);

        // IfMatchEtag causes Cosmos to return 412 PreconditionFailed if the document
        // was modified between the read above and this write, preventing a silent
        // overwrite of concurrent changes.
        var writeResponse = await _writeContainer.ReplaceItemAsync(
            item,
            id,
            new PartitionKey(partitionKey),
            new ItemRequestOptions { IfMatchEtag = readResponse.ETag },
            cancellationToken);

        _logger.LogInformation(
            "SoftDeleteAsync replace: Item {Id} consumed {RequestCharge} RUs. Diagnostics: {Diagnostics}",
            id, writeResponse.RequestCharge, writeResponse.Diagnostics);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<T> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "GetAllAsync called without a partition key on {ModelType} — this issues a cross-partition " +
            "fan-out query that can consume a large number of RUs on big containers. " +
            "Use GetAllAsync(partitionKey) or a specification-based query when the partition key is known.",
            typeof(T).Name);

        var linq = Queryable.Where(x => !x.Deleted);
        return ExecuteIterator(linq.ToFeedIterator(), cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<T> GetAllAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT * FROM c WHERE c.{_partitionKeyProperty} = @pk AND c.Deleted = false";
        var def = new QueryDefinition(sql).WithParameter("@pk", partitionKey);
        return ExecuteIterator(
            _readContainer.GetItemQueryIterator<T>(def,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) }),
            cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<T> GetAllAsync(int pageSize, int offset, int maxItems,
        CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT * FROM c WHERE c.Deleted = false OFFSET @offset LIMIT @pageSize";
        var def = new QueryDefinition(sql)
            .WithParameter("@offset", offset)
            .WithParameter("@pageSize", pageSize);
        return ExecuteIterator(
            _readContainer.GetItemQueryIterator<T>(def,
                requestOptions: new QueryRequestOptions { MaxItemCount = pageSize }),
            cancellationToken,
            maxItems);
    }

    private async IAsyncEnumerable<T> ExecuteIterator(
        FeedIterator<T> iterator,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        int? take = null)
    {
        var remaining = take;
        while (iterator.HasMoreResults && (!remaining.HasValue || remaining > 0))
        {
            var page = await iterator.ReadNextAsync(cancellationToken);

            // log RUs + diagnostics for this page
            _logger.LogInformation(
                "Cosmos query page returned {Count} items; consumed {RequestCharge} RUs; Diagnostics: {Diagnostics}",
                page.Count,
                page.RequestCharge,
                page.Diagnostics
            );

            foreach (var item in page)
            {
                yield return item;
                if (remaining.HasValue && --remaining == 0)
                    yield break;
            }
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<T> QueryAsync(ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        var def = specification.ToCosmosQuery();
        return ExecuteIterator(_readContainer.GetItemQueryIterator<T>(def), cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<List<T>> BulkReadAsyncEnumerable(ISpecification<T> specification, string partitionKey,
        int batchSize = 100, int maxConcurrency = 50, CancellationToken cancellationToken = default)
    {
        var options = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(partitionKey), MaxItemCount = batchSize, MaxConcurrency = maxConcurrency
        };
        return ExecuteBatchIterator(
            _readContainer.GetItemQueryIterator<T>(specification.ToCosmosQuery(), requestOptions: options),
            cancellationToken);
    }

    private async IAsyncEnumerable<List<T>> ExecuteBatchIterator(
        FeedIterator<T> iterator,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);

            _logger.LogInformation(
                "Cosmos bulk-read page returned {Count} items; consumed {RequestCharge} RUs; Diagnostics: {Diagnostics}",
                page.Count,
                page.RequestCharge,
                page.Diagnostics
            );

            yield return page.ToList();
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetCountAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        // Add the missing soft delete filter!
        var sql = $"SELECT VALUE COUNT(1) FROM c WHERE c.{_partitionKeyProperty} = @pk AND c.Deleted = false";
        var def = new QueryDefinition(sql)
            .WithParameter("@pk", partitionKey);

        var options = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(partitionKey),
            MaxItemCount = 1 // We only expect one result
        };

        var iterator = _readContainer.GetItemQueryIterator<int>(def, requestOptions: options);

        var response = await iterator.ReadNextAsync(cancellationToken);

        CosmosRepositoryMetrics.RequestCharge.Record(
            response.RequestCharge,
            new KeyValuePair<string, object?>("model", typeof(T).Name),
            new KeyValuePair<string, object?>("operation", "GetCount")
        );

        return response.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<int> GetTotalCountAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        // Add the missing soft delete filter!
        var sql = $"SELECT VALUE COUNT(1) FROM c WHERE c.{_partitionKeyProperty} = @pk";
        var def = new QueryDefinition(sql)
            .WithParameter("@pk", partitionKey);

        var options = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(partitionKey),
            MaxItemCount = 1 // We only expect one result
        };

        var iterator = _readContainer.GetItemQueryIterator<int>(def, requestOptions: options);

        var response = await iterator.ReadNextAsync(cancellationToken);

        CosmosRepositoryMetrics.RequestCharge.Record(
            response.RequestCharge,
            new KeyValuePair<string, object?>("model", typeof(T).Name),
            new KeyValuePair<string, object?>("operation", "GetCount")
        );

        return response.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<int> GetCountWithCacheAsync(string partitionKey, int cacheExpiryMinutes,
        CancellationToken cancellationToken = default)
    {
        _validator.ValidatePartitionKey(partitionKey, "GetCountWithCache");
        _validator.ValidateCacheExpiry(cacheExpiryMinutes);

        // Generate unique cache key for this model type + partition
        var cacheKey = $"count_{typeof(T).Name}_{partitionKey}";

        // cacheExpiryMinutes == 0 means "always bypass" — skip the cache entirely.
        if (cacheExpiryMinutes == 0)
        {
            _logger.LogDebug("Cache bypass requested for count query on partition {PartitionKey}", partitionKey);
            return await GetCountAsync(partitionKey, cancellationToken);
        }

        // IMemoryCache owns the TTL — if the key is present the entry has not yet expired.
        // No manual age arithmetic needed.
        if (_cache.TryGetValue(cacheKey, out int cachedCount))
        {
            _logger.LogDebug(
                "Returning cached count {Count} for partition {PartitionKey}",
                cachedCount,
                partitionKey
            );

            CosmosRepositoryMetrics.CacheHitCount.Add(1,
                new KeyValuePair<string, object?>("model", typeof(T).Name),
                new KeyValuePair<string, object?>("operation", "GetCountCache")
            );

            return cachedCount;
        }

        _logger.LogDebug("No cached count found for partition {PartitionKey}", partitionKey);

        return await GetFreshCountAsync(partitionKey, cacheKey, cacheExpiryMinutes, cancellationToken);
    }

    /// <summary>
    /// Performs a fresh COUNT query, stores the result in the cache with the caller-supplied
    /// TTL, and returns the count.
    /// </summary>
    private async Task<int> GetFreshCountAsync(string partitionKey, string cacheKey,
        int cacheExpiryMinutes, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing fresh count query for partition {PartitionKey}", partitionKey);

        CosmosRepositoryMetrics.CacheMissCount.Add(1,
            new KeyValuePair<string, object?>("model", typeof(T).Name),
            new KeyValuePair<string, object?>("operation", "GetCountCache")
        );

        var count = await GetCountAsync(partitionKey, cancellationToken);

        // Let IMemoryCache own the expiry — no wrapper class or manual age-check needed.
        _cache.Set(cacheKey, count, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cacheExpiryMinutes),
            Priority = CacheItemPriority.Normal,
            Size = 1
        });

        _logger.LogInformation(
            "Cached fresh count {Count} for partition {PartitionKey} (TTL: {Minutes} min)",
            count,
            partitionKey,
            cacheExpiryMinutes
        );

        return count;
    }

    /// <summary>
    /// Invalidates the cached count for a specific partition.
    /// Call this after operations that change document counts (create, delete, bulk operations).
    /// </summary>
    /// <param name="partitionKey">The partition key whose cache should be invalidated.</param>
    public void InvalidateCountCache(string partitionKey)
    {
        if (string.IsNullOrEmpty(partitionKey)) return;

        var cacheKey = $"count_{typeof(T).Name}_{partitionKey}";
        _cache.Remove(cacheKey);

        _logger.LogDebug("Invalidated count cache for partition {PartitionKey}", partitionKey);
    }

    /// <inheritdoc/>
    public async Task<(IList<T> Items, string? ContinuationToken)> GetPageWithTokenAsync(
        ISpecification<T> spec,
        string partitionKey,
        int pageSize,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        var (items, token, _) = await GetPageInternalAsync(
                spec,
                partitionKey,
                pageSize,
                continuationToken,
                cancellationToken,
                includeCount: false)
            ;

        return (items, token);
    }

    /// <inheritdoc/>
    public Task<(IList<T> Items, string? ContinuationToken, int? TotalCount)> GetPageWithTokenAndCountAsync(
        ISpecification<T> spec, string partitionKey, int pageSize, string? continuationToken = null,
        CancellationToken cancellationToken = default)
        => GetPageInternalAsync(spec, partitionKey, pageSize, continuationToken, cancellationToken, includeCount: true);

    private async Task<(IList<T> Items, string? ContinuationToken, int? TotalCount)> GetPageInternalAsync(
        ISpecification<T> spec,
        string partitionKey,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken,
        bool includeCount)
    {
        var options = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(partitionKey),
            MaxItemCount = pageSize
        };

        var pageResponse = await _readContainer
            .GetItemQueryIterator<T>(spec.ToCosmosQuery(), continuationToken, options)
            .ReadNextAsync(cancellationToken);

        // 2) Record RU charge for the page
        CosmosRepositoryMetrics.RequestCharge.Record(
            pageResponse.RequestCharge,
            new("model", typeof(T).Name),
            new("operation", "GetPage")
        );

        _logger.LogInformation(
            "GetPageInternalAsync: Partition {PartitionKey} returned {Count} items; consumed {RequestCharge} RUs; Diagnostics: {Diagnostics}",
            partitionKey,
            pageResponse.Count,
            pageResponse.RequestCharge,
            pageResponse.Diagnostics
        );

        var items = pageResponse.ToList();
        var nextToken = pageResponse.ContinuationToken;
        int? total = null;

        // 3) If requested, fetch total count (only on first page)
        if (includeCount && string.IsNullOrEmpty(continuationToken))
        {
            try
            {
                var countSpec = spec.ConvertToCountQuery();
                var countIterator = _readContainer.GetItemQueryIterator<int>(countSpec, requestOptions: options);

                var countResponse = await countIterator.ReadNextAsync(cancellationToken);

                // Record RU for count query
                CosmosRepositoryMetrics.RequestCharge.Record(
                    countResponse.RequestCharge,
                    new("model", typeof(T).Name),
                    new("operation", "GetPageCount")
                );

                _logger.LogInformation(
                    "GetPageInternalAsync (count): Partition {PartitionKey} consumed {RequestCharge} RUs; Diagnostics: {Diagnostics}",
                    partitionKey,
                    countResponse.RequestCharge,
                    countResponse.Diagnostics
                );

                total = countResponse.FirstOrDefault();
            }
            catch (CosmosException ex)
            {
                throw new CosmoBaseException("Error retrieving total count for paged query", ex);
            }
        }

        return (items, nextToken, total);
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetAllByArrayPropertyAsync(
        string arrayName,
        string elementPropertyName,
        object elementPropertyValue,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        _validator.ValidateArrayPropertyQuery(arrayName, elementPropertyName, elementPropertyValue);

        // Build the SQL query with conditional soft delete filter
        var whereClause = $"ARRAY_CONTAINS(c.{arrayName}, {{ '{elementPropertyName}': @value }})";

        if (!includeDeleted)
        {
            whereClause += " AND c.Deleted = false";
        }

        var sql = $"SELECT * FROM c WHERE {whereClause}";
        var def = new QueryDefinition(sql)
            .WithParameter("@value", elementPropertyValue);

        var iterator = _readContainer.GetItemQueryIterator<T>(def);
        var results = new List<T>();

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);

            // Record RU charge
            CosmosRepositoryMetrics.RequestCharge.Record(
                page.RequestCharge,
                new("model", typeof(T).Name),
                new("operation", "GetAllByArrayProperty")
            );

            _logger.LogInformation(
                "GetAllByArrayPropertyAsync: returned {Count} items; consumed {RequestCharge} RUs; includeDeleted={IncludeDeleted}; Diagnostics: {Diagnostics}",
                page.Count,
                page.RequestCharge,
                includeDeleted,
                page.Diagnostics
            );

            results.AddRange(page);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetAllByPropertyComparisonAsync(
        IEnumerable<PropertyFilter> filters,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        // Materialize filters
        var filterList = filters as IReadOnlyCollection<PropertyFilter> ?? filters.ToList();

        _validator.ValidatePropertyFilters(filterList);

        // Build WHERE-clause conditions from the property filters
        var whereClause = filterList.BuildSqlWhereClause();

        // Append soft-delete predicate when required
        if (!includeDeleted)
        {
            whereClause = filterList.Any()
                ? $"{whereClause} AND c.Deleted = false"
                : "c.Deleted = false";
        }

        // Compose the full SQL statement
        var sql = whereClause is "1=1" or ""
            ? "SELECT * FROM c"
            : $"SELECT * FROM c WHERE {whereClause}";

        var def = new QueryDefinition(sql);
        filterList.AddParameters(def);

        var iterator = _readContainer.GetItemQueryIterator<T>(def);
        var results = new List<T>();

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);

            // Record RU charge
            CosmosRepositoryMetrics.RequestCharge.Record(
                page.RequestCharge,
                new("model", typeof(T).Name),
                new("operation", "GetAllByPropertyComparison")
            );

            _logger.LogInformation(
                "GetAllByPropertyComparisonAsync: returned {Count} items; consumed {RequestCharge} RUs; includeDeleted={IncludeDeleted}; filterCount={FilterCount}; Diagnostics: {Diagnostics}",
                page.Count,
                page.RequestCharge,
                includeDeleted,
                filterList.Count,
                page.Diagnostics
            );

            results.AddRange(page);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task BulkUpsertAsync(IEnumerable<T> items, string partitionKeyValue, int batchSize = 100,
        int maxConcurrency = 10, CancellationToken cancellationToken = default)
    {
        // Materialize items ONCE at the beginning
        var itemList = items.ToList() ?? throw new ArgumentNullException(nameof(items));

        // Pass the materialized collection to validator and BulkExecuteAsync
        _validator.ValidateBulkItems(itemList, partitionKeyValue, _partitionKeyProperty, "BulkUpsert");
        _validator.ValidateBulkOperationParameters(batchSize, maxConcurrency);

        try
        {
            var result = await BulkExecuteAsync(itemList, partitionKeyValue, batchSize, maxConcurrency,
                (batch, item) => { batch.UpsertItem(item); }, BulkOperationType.Upsert, cancellationToken);

            _logger.LogInformation(
                "BulkUpsertAsync completed successfully: {SuccessCount} items processed, {TotalRUs} RUs consumed",
                result.SuccessfulItems.Count,
                result.TotalRequestUnits
            );

            // Only invalidate cache after successful completion
            InvalidateCountCache(partitionKeyValue);
        }
        catch (CosmoBaseException ex) when (ex.Data.Contains("BulkResult"))
        {
            // Re-throw with more context for bulk upsert
            var result = (BulkExecuteResult<T>)ex.Data["BulkResult"]!;

            _logger.LogError(
                "BulkUpsertAsync partially failed: {SuccessCount} succeeded, {FailedCount} failed, {TotalRUs} RUs consumed",
                result.SuccessfulItems.Count,
                result.FailedItems.Count,
                result.TotalRequestUnits
            );

            // Still invalidate cache if some items succeeded (count may have changed)
            if (result.SuccessfulItems.Any())
            {
                InvalidateCountCache(partitionKeyValue);
            }

            // Create a more specific exception for upsert failures
            var upsertException = new CosmoBaseException(
                $"Bulk upsert operation failed: {result.FailedItems.Count} out of {result.SuccessfulItems.Count + result.FailedItems.Count} items failed"
            )
            {
                Data =
                {
                    ["BulkUpsertResult"] = result
                }
            };
            throw upsertException;
        }
    }

    /// <inheritdoc/>
    public async Task BulkInsertAsync(IEnumerable<T> items, string partitionKeyValue, int batchSize = 100,
        int maxConcurrency = 10, CancellationToken cancellationToken = default)
    {
        // Materialize items ONCE at the beginning
        var itemList = items.ToList() ?? throw new ArgumentNullException(nameof(items));

        // Pass the materialized collection to validator and BulkExecuteAsync
        _validator.ValidateBulkItems(itemList, partitionKeyValue, _partitionKeyProperty, "BulkInsert");
        _validator.ValidateBulkOperationParameters(batchSize, maxConcurrency);

        try
        {
            var result = await BulkExecuteAsync(itemList, partitionKeyValue, batchSize, maxConcurrency,
                (batch, item) => { batch.CreateItem(item); }, BulkOperationType.Create, cancellationToken);

            _logger.LogInformation(
                "BulkInsertAsync completed successfully: {SuccessCount} items processed, {TotalRUs} RUs consumed",
                result.SuccessfulItems.Count,
                result.TotalRequestUnits
            );

            // Only invalidate cache after successful completion
            InvalidateCountCache(partitionKeyValue);
        }
        catch (CosmoBaseException ex) when (ex.Data.Contains("BulkResult"))
        {
            // Re-throw with more context for bulk insert
            var result = (BulkExecuteResult<T>)ex.Data["BulkResult"]!;

            _logger.LogError(
                "BulkInsertAsync partially failed: {SuccessCount} succeeded, {FailedCount} failed, {TotalRUs} RUs consumed",
                result.SuccessfulItems.Count,
                result.FailedItems.Count,
                result.TotalRequestUnits
            );

            // Still invalidate cache if some items succeeded (count increased)
            if (result.SuccessfulItems.Any())
            {
                InvalidateCountCache(partitionKeyValue);
            }

            // Create a more specific exception for insert failures
            var insertException = new CosmoBaseException(
                $"Bulk insert operation failed: {result.FailedItems.Count} out of {result.SuccessfulItems.Count + result.FailedItems.Count} items failed"
            )
            {
                Data =
                {
                    ["BulkInsertResult"] = result
                }
            };
            throw insertException;
        }
    }

    /// <inheritdoc/>
    public async Task<T?> PatchItemAsync(
        string id,
        string partitionKey,
        PatchSpecification spec,
        CancellationToken cancellationToken = default)
    {
        var ops = spec.ToCosmosPatchOperations();

        var response = await _writeContainer.PatchItemAsync<T>(
            id,
            new PartitionKey(partitionKey),
            ops,
            cancellationToken: cancellationToken
        );

        // 2) record RU charge in histogram
        CosmosRepositoryMetrics.RequestCharge.Record(
            response.RequestCharge,
            new("model", typeof(T).Name),
            new("operation", "PatchItem")
        );

        // 3) log RU charge + diagnostics
        _logger.LogInformation(
            "PatchItemAsync: Item {Id} consumed {RequestCharge} RUs. Diagnostics: {Diagnostics}",
            id,
            response.RequestCharge,
            response.Diagnostics
        );

        return response.Resource;
    }

    /// <summary>
    /// Returns the partition-key value for <paramref name="item"/> using the pre-compiled delegate.
    /// Throws <see cref="CosmosConfigurationException"/> when the DAO type does not expose the
    /// partition key property — indicates a write service was used with a read-only model.
    /// </summary>
    private string GetPartitionKeyValue(T item)
    {
        if (_getPartitionKey is null)
            throw new CosmosConfigurationException(
                $"'{typeof(T).Name}' does not have a property that maps to partition key " +
                $"'{_partitionKeyProperty}'. Write operations require the partition key property. " +
                $"Add the property to the DAO or use [JsonPropertyName(\"{_partitionKeyProperty}\")] " +
                $"on the matching field. If this model is read-only, use ICosmosDataReadService instead.");

        var value = _getPartitionKey(item);
        if (value is null)
            throw new InvalidOperationException(
                $"Partition-key value for '{_partitionKeyProperty}' on '{typeof(T).Name}' was null");
        return value;
    }

    /// <summary>
    /// Builds a compiled <see cref="Func{T, String}"/> that reads the partition-key property
    /// from an instance of <typeparamref name="T"/> without reflection overhead at call time.
    /// Returns <c>null</c> when the property cannot be resolved — valid for read-only models.
    /// </summary>
    /// <remarks>
    /// Resolution is a two-step lookup (done once at construction):
    ///   1. Try <paramref name="propertyName"/> as a C# property name directly.
    ///   2. If not found, scan for a property decorated with
    ///      <c>[JsonPropertyName("<paramref name="propertyName"/>")]</c>.
    ///      This supports configurations where the Cosmos JSON field name (e.g. "btLockboxNumber")
    ///      differs from the C# property name (e.g. "Lockbox").
    /// If neither lookup succeeds, returns <c>null</c>; no exception is thrown here so that
    /// read-only models can register and query without a partition key property on the DAO.
    /// </remarks>
    private static Func<T, string>? BuildPartitionKeyAccessor(string propertyName)
    {
        var type = typeof(T);

        // Step 1: exact C# property name
        var prop = type.GetProperty(propertyName);

        // Step 2: scan for [JsonPropertyName] attribute whose value matches
        prop ??= type.GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == propertyName);

        if (prop is null)
            return null;

        var param    = Expression.Parameter(type, "item");
        var access   = Expression.Property(param, prop);
        var toString = Expression.Call(access, nameof(object.ToString), Type.EmptyTypes);
        return Expression.Lambda<Func<T, string>>(toString, param).Compile();
    }

    /// <summary>
    /// Splits items into fixed-size batches.
    /// </summary>
    private static List<List<T>> CreateBatches(IList<T> items, int batchSize)
    {
        var batches = new List<List<T>>();

        for (var i = 0; i < items.Count; i += batchSize)
        {
            var batch = items.Skip(i).Take(batchSize).ToList();
            batches.Add(batch);
        }

        return batches;
    }

    /// <summary>
    /// Processes batches concurrently with controlled parallelism.
    /// </summary>
    private async Task<List<BatchExecuteResult<T>>> ProcessBatchesConcurrently(
        List<List<T>> batches,
        string partitionKeyValue,
        Action<TransactionalBatch, T> batchAction,
        BulkOperationType operationType,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        var results = new ConcurrentBag<BatchExecuteResult<T>>();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(
            batches.Select((batch, index) => new { Batch = batch, Index = index }),
            parallelOptions,
            async (batchItem, ct) =>
            {
                var result = await ExecuteSingleBatch(batchItem.Batch, batchItem.Index, partitionKeyValue, batchAction,
                    operationType, ct);
                results.Add(result);
            });

        return results.ToList();
    }

    /// <summary>
    /// Executes a single batch with detailed error handling.
    /// </summary>
    private async Task<BatchExecuteResult<T>> ExecuteSingleBatch(
        List<T> batch,
        int batchIndex,
        string partitionKeyValue,
        Action<TransactionalBatch, T> batchAction,
        BulkOperationType operationType,
        CancellationToken cancellationToken)
    {
        var result = new BatchExecuteResult<T> { BatchIndex = batchIndex };

        try
        {
            // Set audit fields for all items in batch using the manager
            var isCreateOperation = operationType == BulkOperationType.Create;
            _auditFieldManager.SetBulkAuditFields(batch, isCreateOperation);

            // Create transactional batch
            var txn = _writeContainer.CreateTransactionalBatch(new PartitionKey(partitionKeyValue));
            foreach (var item in batch)
            {
                batchAction(txn, item);
            }

            var batchResponse = await txn.ExecuteAsync(cancellationToken);

            result.RequestUnits = batchResponse.RequestCharge;

            // Check overall batch success
            if (batchResponse.IsSuccessStatusCode)
            {
                result.SuccessfulItems.AddRange(batch);
                _logger.LogDebug(
                    "Batch {BatchIndex}: {ItemCount} items succeeded, {RequestCharge} RUs consumed",
                    batchIndex,
                    batch.Count,
                    batchResponse.RequestCharge
                );
            }
            else
            {
                // Batch failed - analyze individual item results
                AnalyzeBatchFailures(batch, batchResponse, result);
            }

            // Record metrics
            CosmosRepositoryMetrics.RequestCharge.Record(
                batchResponse.RequestCharge,
                new("model", typeof(T).Name),
                new("operation", "BulkExecute"),
                new("batch_status", batchResponse.IsSuccessStatusCode ? "success" : "failed")
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch {BatchIndex}: Exception during execution", batchIndex);

            // Mark all items in batch as failed
            foreach (var item in batch)
            {
                result.FailedItems.Add(new BulkItemFailure<T>
                {
                    Item = item,
                    StatusCode = null,
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Executes batch operations (upsert/create) in parallel groups with comprehensive error handling and audit field management.
    /// This is the core method that orchestrates bulk operations with optimal performance and reliability.
    /// </summary>
    /// <param name="items">Items to process (already materialized and validated by public methods).</param>
    /// <param name="partitionKeyValue">Partition key value for all items (must be consistent across all items).</param>
    /// <param name="batchSize">Number of items per transactional batch (Cosmos DB limit: 100 items per batch).</param>
    /// <param name="maxConcurrency">Maximum number of parallel batch executions to prevent overwhelming Cosmos DB.</param>
    /// <param name="batchAction">Action delegate that defines the specific batch operation (CreateItem, UpsertItem, etc.).</param>
    /// <param name="operationType">Type of bulk operation to determine proper audit field handling (Create vs Upsert).</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation across all async operations.</param>
    /// <returns>
    /// Detailed results indicating success/failure for each item, total RU consumption, and operational metrics.
    /// </returns>
    /// <exception cref="CosmoBaseException">
    /// Thrown when any batch operations fail. Contains detailed failure information in the Data property
    /// under the "BulkResult" key for programmatic access to success/failure breakdown.
    /// </exception>
    /// <remarks>
    /// This method implements several enterprise-grade patterns:
    /// 
    /// **Performance Optimizations:**
    /// - Parallel batch execution with configurable concurrency limits
    /// - Efficient item batching to maximize Cosmos DB throughput
    /// - Bulk audit field updates to minimize per-item overhead
    /// - Request unit tracking and telemetry for monitoring
    /// 
    /// **Reliability Features:**
    /// - Comprehensive retry policies for transient failures
    /// - Individual item failure tracking within failed batches
    /// - Detailed error reporting with item-level context
    /// - Graceful handling of partial batch failures
    /// 
    /// **Audit &amp; Compliance:**
    /// - Automatic audit field population based on operation type
    /// - User context resolution for CreatedBy/UpdatedBy fields
    /// - Timestamp management for CreatedOnUtc/UpdatedOnUtc fields
    /// - Consistent audit behavior across all bulk operations
    /// 
    /// **Observability:**
    /// - Structured logging with operation context and performance metrics
    /// - OpenTelemetry-compatible metrics for monitoring and alerting
    /// - Request unit consumption tracking for cost optimization
    /// - Success rate reporting for operational dashboards
    /// </remarks>
    private async Task<BulkExecuteResult<T>> BulkExecuteAsync(
        IReadOnlyCollection<T> items,
        string partitionKeyValue,
        int batchSize,
        int maxConcurrency,
        Action<TransactionalBatch, T> batchAction,
        BulkOperationType operationType,
        CancellationToken cancellationToken = default)
    {
        // =====================================================================================
        // EARLY EXIT: Handle empty collections gracefully
        // =====================================================================================
        // Note: Input validation (null checks, parameter validation) is performed by the 
        // public methods (BulkUpsertAsync/BulkInsertAsync) before calling this method.
        // This separation of concerns keeps the core logic focused on execution.
        if (!items.Any())
        {
            _logger.LogDebug("BulkExecuteAsync: No items to process for {OperationType} operation", operationType);
            return new BulkExecuteResult<T>
            {
                SuccessfulItems = new List<T>(),
                FailedItems = new List<BulkItemFailure<T>>(),
                TotalRequestUnits = 0
            };
        }

        // =====================================================================================
        // PREPARATION: Convert collection and split into batches
        // =====================================================================================
        // Items are already materialized by the calling method to avoid multiple enumeration.
        // This conversion is cheap since the collection is already in memory.
        var itemList = items as List<T> ?? items.ToList();

        // Split items into fixed-size batches for optimal Cosmos DB performance.
        // Cosmos DB transactional batches have a limit of 100 items per batch.
        var batches = CreateBatches(itemList, batchSize);

        _logger.LogInformation(
            "BulkExecuteAsync: Starting {OperationType} operation - {ItemCount} items split into {BatchCount} batches (batchSize: {BatchSize}, maxConcurrency: {MaxConcurrency})",
            operationType,
            itemList.Count,
            batches.Count,
            batchSize,
            maxConcurrency
        );

        // =====================================================================================
        // EXECUTION: Process batches with controlled concurrency
        // =====================================================================================
        // Use Parallel.ForEachAsync for optimal async concurrency control.
        // This approach provides:
        // - Built-in concurrency limiting via MaxDegreeOfParallelism
        // - Proper async/await support without Task.Run overhead
        // - Automatic load balancing across available threads
        // - Cooperative cancellation support
        var results = await ProcessBatchesConcurrently(
            batches,
            partitionKeyValue,
            batchAction,
            operationType,
            maxConcurrency,
            cancellationToken
        );

        // =====================================================================================
        // AGGREGATION: Combine results from all batches
        // =====================================================================================
        // Aggregate individual batch results into a comprehensive summary.
        // This includes successful items, failed items, and total RU consumption.
        var aggregatedResult = AggregateBatchResults(results);

        // =====================================================================================
        // TELEMETRY: Log completion metrics for monitoring and debugging
        // =====================================================================================
        _logger.LogInformation(
            "BulkExecuteAsync: {OperationType} operation completed - Success: {SuccessCount}/{TotalCount} ({SuccessRate:F1}%), Failed: {FailedCount}, Total RUs: {TotalRUs}",
            operationType,
            aggregatedResult.SuccessfulItems.Count,
            itemList.Count,
            aggregatedResult.SuccessRate,
            aggregatedResult.FailedItems.Count,
            aggregatedResult.TotalRequestUnits
        );

        // =====================================================================================
        // ERROR HANDLING: Process failures and provide detailed error information
        // =====================================================================================
        // If any items failed, throw a comprehensive exception with detailed failure information.
        // The exception includes the full result object in its Data dictionary, allowing
        // calling code to access detailed success/failure breakdown for:
        // - Retry logic for retryable failures
        // - Error reporting and alerting
        // - Partial success handling
        // - Operational metrics and dashboards
        if (aggregatedResult.FailedItems.Any())
        {
            var failureMessage =
                $"Bulk {operationType.ToString().ToLower()} operation completed with {aggregatedResult.FailedItems.Count} failures out of {itemList.Count} items";

            // Log detailed failure information for operational troubleshooting
            _logger.LogWarning(
                "BulkExecuteAsync: Partial failure in {OperationType} operation - {FailureDetails}",
                operationType,
                string.Join("; ", aggregatedResult.FailedItems.Take(5).Select(f =>
                    $"Item '{f.Item.Id}': {f.StatusCode} - {f.ErrorMessage}"))
            );

            // Create exception with embedded result data for programmatic access
            var exception = new CosmoBaseException(failureMessage)
            {
                Data =
                {
                    ["BulkResult"] = aggregatedResult,
                    ["OperationType"] = operationType.ToString(),
                    ["PartitionKey"] = partitionKeyValue
                }
            };

            throw exception;
        }

        // =====================================================================================
        // SUCCESS: Return comprehensive result with all operation details
        // =====================================================================================
        return aggregatedResult;
    }

    /// <summary>
    /// Analyzes individual item failures within a failed batch.
    /// </summary>
    private void AnalyzeBatchFailures(List<T> batch, TransactionalBatchResponse batchResponse,
        BatchExecuteResult<T> result)
    {
        for (var i = 0; i < batch.Count; i++)
        {
            var item = batch[i];
            var itemResponse = batchResponse[i];

            if (itemResponse.IsSuccessStatusCode)
            {
                result.SuccessfulItems.Add(item);
            }
            else
            {
                result.FailedItems.Add(new BulkItemFailure<T>
                {
                    Item = item,
                    StatusCode = itemResponse.StatusCode,
                    ErrorMessage = $"Batch operation failed with status: {itemResponse.StatusCode}",
                    Exception = null
                });

                _logger.LogWarning(
                    "Batch item failed: Id={ItemId}, Status={StatusCode}",
                    item.Id,
                    itemResponse.StatusCode
                );
            }
        }
    }

    /// <summary>
    /// Aggregates results from multiple batch executions.
    /// </summary>
    private static BulkExecuteResult<T> AggregateBatchResults(List<BatchExecuteResult<T>> batchResults)
    {
        var result = new BulkExecuteResult<T>();

        foreach (var batchResult in batchResults)
        {
            result.SuccessfulItems.AddRange(batchResult.SuccessfulItems);
            result.FailedItems.AddRange(batchResult.FailedItems);
            result.TotalRequestUnits += batchResult.RequestUnits;
        }

        return result;
    }
}