using System.Net;
using System.Runtime.CompilerServices;
using CosmoBase.Abstractions.Configuration;
using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Exceptions;
using CosmoBase.Abstractions.Filters;
using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Extensions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace CosmoBase.Repositories;

/// <summary>
/// Provides low-level CRUD and query operations against a Cosmos DB container.
/// Use higher-level services for business logic and DTO mapping.
/// </summary>
/// <typeparam name="T">The document model type stored in Cosmos, must implement <see cref="ICosmosDataModel"/> and have a parameterless constructor.</typeparam>
public class CosmosRepository<T> : ICosmosRepository<T> where T : class, ICosmosDataModel, new()
{
    // Define a static retry policy for transient CosmosExceptions
    private readonly AsyncRetryPolicy<ItemResponse<T>> _cosmosRetryPolicy;

    // Define a static retry policy for feed-iterator pages
    private readonly IAsyncPolicy _cosmosFeedRetryPolicy;

    // Define a static retry policy for transactional batch calls
    private readonly AsyncRetryPolicy<TransactionalBatchResponse> _cosmosBatchRetryPolicy;

    private static bool IsTransient(CosmosException ex) =>
        ex.StatusCode == HttpStatusCode.RequestTimeout
        || ex.StatusCode == HttpStatusCode.TooManyRequests
        || ex.StatusCode == HttpStatusCode.ServiceUnavailable
        || ex.StatusCode == HttpStatusCode.InternalServerError;

    private readonly Container _readContainer;
    private readonly Container _writeContainer;
    private readonly string _partitionKeyProperty;
    private readonly ILogger<CosmosRepository<T>> _logger;
    //private readonly TelemetryClient _telemetryClient;

    /// <summary>
    /// Initializes a new instance using <paramref name="configuration"/> to locate containers.
    /// </summary>
    /// <param name="configuration">Cosmos DB configuration settings.</param>
    /// <param name="logger">The logger for this repository</param>
    /// <exception cref="CosmosConfigurationException">Thrown if configuration for <typeparamref name="T"/> is missing.</exception>
    public CosmosRepository(CosmosConfiguration configuration, ILogger<CosmosRepository<T>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Item‐level retry (Create/Read/Replace/Upsert/Delete/Patch)
        _cosmosRetryPolicy =
            Policy<ItemResponse<T>>
                .Handle<CosmosException>(IsTransient)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, delay, retryCount, context) =>
                    {
                        // outcome.Exception is the actual CosmosException
                        var ex = outcome.Exception!;

                        // increment retry counter
                        CosmosRepositoryMetrics.RetryCount.Add(
                            1,
                            new("model", typeof(T).Name),
                            new("operation", "ItemOperation")
                        );

                        _logger.LogWarning(
                            ex,
                            "Cosmos {Operation} retry {RetryCount} after {Delay}",
                            context.OperationKey ?? "ItemOperation",
                            retryCount,
                            delay
                        );
                    }
                );

        // FeedIterator‐level retry (GetAllAsync, QueryAsync, paging, etc.)
        _cosmosFeedRetryPolicy = Policy
            .Handle<CosmosException>(IsTransient)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, delay, retryCount, context) =>
                {
                    // exception is the actual CosmosException
                    CosmosRepositoryMetrics.RetryCount.Add(
                        1,
                        new("model", typeof(T).Name),
                        new("operation", "FeedOperation")
                    );
                    
                    _logger.LogWarning(
                        exception,
                        "Cosmos {Operation} retry {RetryCount} after {Delay}",
                        context.OperationKey ?? "FeedOperation",
                        retryCount,
                        delay
                    );
                }
            );
        
        // Transactional‐batch retry (BulkUpsert/BulkInsert)
        _cosmosBatchRetryPolicy = Policy<TransactionalBatchResponse>
            .Handle<CosmosException>(IsTransient)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, delay, retryCount, context) =>
                {
                    var ex = outcome.Exception!;

                    CosmosRepositoryMetrics.RetryCount.Add(
                        1,
                        new("model", typeof(T).Name),
                        new("operation", "BulkOperation")
                    );

                    _logger.LogWarning(
                        ex,
                        "Cosmos {Operation} retry {RetryCount} after {Delay}",
                        context.OperationKey ?? "BulkOperation",
                        retryCount,
                        delay
                    );
                }
            );

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

        var readClientConfig = configuration.CosmosClientConfigurations
            .FirstOrDefault(x => x.Name.Equals(modelConfig.ReadCosmosClientConfigurationName,
                StringComparison.InvariantCultureIgnoreCase));
        if (readClientConfig == null)
        {
            _logger.LogError("CosmosConfiguration: no read client config named {ReadConfigName} for model {ModelName}",
                modelConfig.ReadCosmosClientConfigurationName, modelName);
            throw new CosmosConfigurationException($"No read client config for {modelName}");
        }

        var writeClientConfig = configuration.CosmosClientConfigurations
            .FirstOrDefault(x => x.Name.Equals(modelConfig.WriteCosmosClientConfigurationName,
                StringComparison.InvariantCultureIgnoreCase));
        if (writeClientConfig == null)
        {
            _logger.LogError(
                "CosmosConfiguration: no write client config named {WriteConfigName} for model {ModelName}",
                modelConfig.WriteCosmosClientConfigurationName, modelName);
            throw new CosmosConfigurationException($"No write client config for {modelName}");
        }

        var readClient = new CosmosClientBuilder(readClientConfig.ConnectionString)
            .WithThrottlingRetryOptions(TimeSpan.FromSeconds(15), 10000)
            .WithBulkExecution(true)
            .Build();
        _readContainer = readClient.GetContainer(modelConfig.DatabaseName, modelConfig.CollectionName);

        var writeClient = new CosmosClientBuilder(writeClientConfig.ConnectionString)
            .WithThrottlingRetryOptions(TimeSpan.FromSeconds(15), 10000)
            .WithBulkExecution(true)
            .Build();
        _writeContainer = writeClient.GetContainer(modelConfig.DatabaseName, modelConfig.CollectionName);
    }

    /// <inheritdoc/>
    public IQueryable<T> Queryable => _readContainer.GetItemLinqQueryable<T>(true);

    /// <inheritdoc/>
    public async Task<T?> GetItemAsync(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _cosmosRetryPolicy.ExecuteAsync(() =>
                _readContainer.ReadItemAsync<T>(
                    id,
                    new PartitionKey(partitionKey),
                    cancellationToken: cancellationToken
                )
            );

            _logger.LogInformation(
                "GetItemAsync: Item {Id} consumed {RequestCharge} RUs. Diagnostics: {Diagnostics}",
                id,
                response.RequestCharge,
                response.Diagnostics
            );

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<T> CreateItemAsync(T item, CancellationToken cancellationToken = default)
    {
        item.CreatedOnUtc = DateTime.UtcNow;
        item.UpdatedOnUtc = DateTime.UtcNow;
        item.Deleted = false;

        // 2a) Execute under retry policy
        var response = await _cosmosRetryPolicy.ExecuteAsync(() =>
            _writeContainer.CreateItemAsync(
                item,
                new PartitionKey(GetPartitionKeyValue(item)),
                cancellationToken: cancellationToken
            )
        );

        // 2b) Log RU charge and diagnostics
        _logger.LogInformation(
            "CreateItemAsync: Item {Id} consumed {RequestCharge} RUs. Diagnostics: {Diagnostics}",
            response.Resource.Id,
            response.RequestCharge,
            response.Diagnostics
        );

        return response.Resource;
    }

    /// <inheritdoc/>
    public async Task<T> ReplaceItemAsync(T item, CancellationToken cancellationToken = default)
    {
        item.UpdatedOnUtc = DateTime.UtcNow;

        // wrap in retry
        var response = await _cosmosRetryPolicy.ExecuteAsync(() =>
            _writeContainer.ReplaceItemAsync(
                item,
                item.Id,
                new PartitionKey(GetPartitionKeyValue(item)),
                cancellationToken: cancellationToken
            )
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
        item.UpdatedOnUtc = DateTime.UtcNow;

        // wrap in retry
        var response = await _cosmosRetryPolicy.ExecuteAsync(() =>
            _writeContainer.UpsertItemAsync(
                item,
                new PartitionKey(GetPartitionKeyValue(item)),
                cancellationToken: cancellationToken
            )
        );

        // log RU charge + diagnostics
        _logger.LogInformation(
            "UpsertItemAsync: Item {Id} consumed {RequestCharge} RUs. Diagnostics: {Diagnostics}",
            response.Resource.Id,
            response.RequestCharge,
            response.Diagnostics
        );

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
            return;
        }

        // wrap hard delete in retry policy
        var response = await _cosmosRetryPolicy.ExecuteAsync(() =>
            _writeContainer.DeleteItemAsync<T>(
                id,
                new PartitionKey(partitionKey),
                cancellationToken: cancellationToken
            )
        );

        // log RU charge + diagnostics
        _logger.LogInformation(
            "DeleteItemAsync: Item {Id} consumed {RequestCharge} RUs. Diagnostics: {Diagnostics}",
            id,
            response.RequestCharge,
            response.Diagnostics
        );
    }

    private async Task SoftDeleteAsync(string id, string partitionKey, CancellationToken cancellationToken)
    {
        var item = await GetItemAsync(id, partitionKey, cancellationToken);
        if (item is null) return;
        item.Deleted = true;
        await ReplaceItemAsync(item, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<T> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // x is ICosmosDataModel
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
    public IAsyncEnumerable<T> GetAllAsync(int limit, int offset, int count,
        CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT * FROM c WHERE c.Deleted = false OFFSET @offset LIMIT @limit";
        var def = new QueryDefinition(sql)
            .WithParameter("@offset", offset)
            .WithParameter("@limit", limit);
        return ExecuteIterator(
            _readContainer.GetItemQueryIterator<T>(def,
                requestOptions: new QueryRequestOptions { MaxItemCount = limit }), cancellationToken, count);
    }

    private async IAsyncEnumerable<T> ExecuteIterator(
        FeedIterator<T> iterator,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        int? take = null)
    {
        var remaining = take;
        while (iterator.HasMoreResults && (!remaining.HasValue || remaining > 0))
        {
            // wrap each page read in retry
            var page = await _cosmosFeedRetryPolicy.ExecuteAsync<FeedResponse<T>>(
                (_, ct) => iterator.ReadNextAsync(ct),
                new Context("GetPage"),
                cancellationToken
            );

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
            var page = await _cosmosFeedRetryPolicy.ExecuteAsync<FeedResponse<T>>(
                (_, ct) => iterator.ReadNextAsync(ct),
                new Context("GetPage"),
                cancellationToken
            );

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
        var sql = $"SELECT VALUE COUNT(1) FROM c WHERE c.{_partitionKeyProperty} = @pk";
        var def = new QueryDefinition(sql)
            .WithParameter("@pk", partitionKey);

        var iterator = _readContainer.GetItemQueryIterator<int>(
            def,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) }
        );

        // execute under retry policy
        var response = await _cosmosFeedRetryPolicy.ExecuteAsync<FeedResponse<int>>(
            (_, ct) => iterator.ReadNextAsync(ct),
            new Context("GetCount"),
            cancellationToken
        );

        // record RU charge in histogram
        CosmosRepositoryMetrics.RequestCharge.Record(
            response.RequestCharge,
            new("model", typeof(T).Name),
            new("operation", "GetCount")
        );

        _logger.LogInformation(
            "GetCountAsync: Partition {PartitionKey} consumed {RequestCharge} RUs. Diagnostics: {Diagnostics}",
            partitionKey,
            response.RequestCharge,
            response.Diagnostics
        );

        return response.FirstOrDefault();
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

        // 1) Read the page under retry policy
        var pageResponse = await _cosmosFeedRetryPolicy.ExecuteAsync(() =>
            _readContainer.GetItemQueryIterator<T>(spec.ToCosmosQuery(), continuationToken, options)
                .ReadNextAsync(cancellationToken)
        );

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

                var countResponse = await _cosmosFeedRetryPolicy.ExecuteAsync(() =>
                    countIterator.ReadNextAsync(cancellationToken)
                );

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
        CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT * FROM c WHERE ARRAY_CONTAINS(c.{arrayName}, {{ '{elementPropertyName}': @value }})";
        var def = new QueryDefinition(sql)
            .WithParameter("@value", elementPropertyValue);

        var iterator = _readContainer.GetItemQueryIterator<T>(def);
        var results = new List<T>();

        while (iterator.HasMoreResults)
        {
            // 1) read next page under retry policy
            var page = await _cosmosFeedRetryPolicy.ExecuteAsync<FeedResponse<T>>(
                (_, ct) => iterator.ReadNextAsync(ct),
                new Context("GetPage"),
                cancellationToken
            );

            // 2) record RU charge
            CosmosRepositoryMetrics.RequestCharge.Record(
                page.RequestCharge,
                new("model", typeof(T).Name),
                new("operation", "GetAllByArrayProperty")
            );

            _logger.LogInformation(
                "GetAllByArrayPropertyAsync: returned {Count} items; consumed {RequestCharge} RUs; Diagnostics: {Diagnostics}",
                page.Count,
                page.RequestCharge,
                page.Diagnostics
            );

            results.AddRange(page);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetAllByPropertyComparisonAsync(
        IEnumerable<PropertyFilter> filters,
        CancellationToken cancellationToken = default)
    {
        // materialize filters
        var filterList = filters as IReadOnlyCollection<PropertyFilter> ?? filters.ToList();

        // build SQL + parameters
        var sql = filterList.BuildSqlWhereClause();
        var def = new QueryDefinition(sql);
        filterList.AddParameters(def);

        var iterator = _readContainer.GetItemQueryIterator<T>(def);
        var results = new List<T>();

        while (iterator.HasMoreResults)
        {
            // execute under retry
            var page = await _cosmosFeedRetryPolicy.ExecuteAsync<FeedResponse<T>>(
                (_, ct) => iterator.ReadNextAsync(ct),
                new Context("GetPage"),
                cancellationToken
            );

            // record RU charge
            CosmosRepositoryMetrics.RequestCharge.Record(
                page.RequestCharge,
                new("model", typeof(T).Name),
                new("operation", "GetAllByPropertyComparison")
            );

            _logger.LogInformation(
                "GetAllByPropertyComparisonAsync: returned {Count} items; consumed {RequestCharge} RUs; Diagnostics: {Diagnostics}",
                page.Count,
                page.RequestCharge,
                page.Diagnostics
            );

            results.AddRange(page);
        }

        return results;
    }

    /// <inheritdoc/>
    public Task BulkUpsertAsync(IEnumerable<T> items, string partitionKeyValue, int batchSize = 100,
        int maxConcurrency = 10, CancellationToken cancellationToken = default)
        => BulkExecuteAsync(items, partitionKeyValue, batchSize, maxConcurrency,
            (batch, item) => { batch.UpsertItem(item); }, cancellationToken);

    /// <inheritdoc/>
    public Task BulkInsertAsync(IEnumerable<T> items, string partitionKeyValue, int batchSize = 100,
        int maxConcurrency = 10, CancellationToken cancellationToken = default)
        => BulkExecuteAsync(items, partitionKeyValue, batchSize, maxConcurrency,
            (batch, item) => { batch.CreateItem(item); }, cancellationToken);


    /// <summary>
    /// Executes batch operations (upsert/create) in parallel groups.
    /// </summary>
    /// <param name="items">Items to process.</param>
    /// <param name="partitionKeyValue">Partition key for all items.</param>
    /// <param name="batchSize">Number of items per batch.</param>
    /// <param name="maxConcurrency">Max parallel batches.</param>
    /// <param name="batchAction">Action to apply per item on the transactional batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task BulkExecuteAsync(
        IEnumerable<T> items,
        string partitionKeyValue,
        int batchSize,
        int maxConcurrency,
        Action<TransactionalBatch, T> batchAction,
        CancellationToken cancellationToken = default)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (string.IsNullOrEmpty(partitionKeyValue)) throw new ArgumentNullException(nameof(partitionKeyValue));
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (maxConcurrency <= 0) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

        // 1) Split items into fixed-size batches
        var batches = new List<List<T>>();
        var currentBatch = new List<T>(batchSize);
        foreach (var item in items)
        {
            currentBatch.Add(item);
            if (currentBatch.Count == batchSize)
            {
                batches.Add(currentBatch);
                currentBatch = new List<T>(batchSize);
            }
        }

        if (currentBatch.Count > 0)
            batches.Add(currentBatch);

        // 2) Execute each batch under a throttle
        var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = new List<Task>(batches.Count);

        foreach (var batch in batches)
        {
            await semaphore.WaitAsync(cancellationToken);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var txn = _writeContainer.CreateTransactionalBatch(new PartitionKey(partitionKeyValue));
                    foreach (var item in batch)
                    {
                        batchAction(txn, item);
                        item.UpdatedOnUtc = DateTime.UtcNow;
                    }

                    // execute with retry
                    var batchResponse = await _cosmosBatchRetryPolicy.ExecuteAsync(() =>
                        txn.ExecuteAsync(cancellationToken)
                    );

                    // record RU charge
                    CosmosRepositoryMetrics.RequestCharge.Record(
                        batchResponse.RequestCharge,
                        new("model", typeof(T).Name),
                        new("operation", "BulkExecute")
                    );

                    // log RU charge + diagnostics
                    _logger.LogInformation(
                        "BulkExecuteAsync: batch consumed {RequestCharge} RUs. Diagnostics: {Diagnostics}",
                        batchResponse.RequestCharge,
                        batchResponse.Diagnostics
                    );
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        // 3) Wait for all batches to complete
        await Task.WhenAll(tasks.ToArray());
    }

    /// <inheritdoc/>
    public async Task<T?> PatchItemAsync(
        string id,
        string partitionKey,
        PatchSpecification spec,
        CancellationToken cancellationToken = default)
    {
        var ops = spec.ToCosmosPatchOperations();

        // 1) execute under item‐level retry policy
        var response = await _cosmosRetryPolicy.ExecuteAsync(() =>
            _writeContainer.PatchItemAsync<T>(
                id,
                new PartitionKey(partitionKey),
                ops,
                cancellationToken: cancellationToken
            )
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
    /// Reads the value of the partition-key property (by name) from the given item.
    /// </summary>
    private string GetPartitionKeyValue(T item)
    {
        var prop = typeof(T).GetProperty(_partitionKeyProperty)
                   ?? throw new InvalidOperationException(
                       $"Partition-key property '{_partitionKeyProperty}' not found on type '{typeof(T).Name}'");
        var raw = prop.GetValue(item)
                  ?? throw new InvalidOperationException(
                      $"Partition-key value for '{_partitionKeyProperty}' on '{typeof(T).Name}' was null");
        return raw.ToString()!;
    }

    /*
    private async Task<ItemResponse<T>> ExecWithMetricsAsync(
        Func<Task<ItemResponse<T>>> call, string operation)
    {
        var resp = await _itemPolicy.ExecuteAsync(call);
        SRequestChargeHist.Record(resp.RequestCharge,
            new("model", typeof(T).Name), new("operation", operation));
        _logger.LogInformation("{Op} consumed {RequestCharge} RUs. Diagnostics: {Diag}",
            operation, resp.RequestCharge, resp.Diagnostics);
        return resp;
    }
    */
}