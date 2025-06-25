using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CosmoBase.Abstractions.Configuration;
using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Exceptions;
using CosmoBase.Abstractions.Filters;
using CosmoBase.Abstractions.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Cosmos.Linq;

namespace CosmoBase.Repositories;

/// <inheritdoc/>
public class CosmosRepository<T> : ICosmosRepository<T>
    where T : class
{
    // Dependency Injection
    protected readonly CosmosConfiguration CosmosConfiguration;

    // Queryable to use for executing custom queries
    public IQueryable<T> Queryable => _readContainer.GetItemLinqQueryable<T>(true);

    // Container for model
    private readonly Container _readContainer;
    private readonly Container _writeContainer;
    private readonly string _partitionKeyProperty;

    public CosmosRepository(CosmosConfiguration cosmosConfiguration)
    {
        this.CosmosConfiguration = cosmosConfiguration;

        // From the configuration we now grab the model config for the name of the model
        var modelName = typeof(T).Name;

        var modelConfig = cosmosConfiguration
            .CosmosModelConfigurations
            .FirstOrDefault(x => x.ModelName.Equals(modelName, StringComparison.InvariantCultureIgnoreCase));

        if (modelConfig == null)
            throw new CosmosConfigurationException($"No configuration found for model {modelName}");

        this._partitionKeyProperty = modelConfig.PartitionKey;

        // Find the read and write clients for this model
        var readClientConfig = cosmosConfiguration
            .CosmosClientConfigurations
            .FirstOrDefault(x => x.Name.Equals(
                modelConfig.ReadCosmosClientConfigurationName,
                StringComparison.CurrentCultureIgnoreCase
            ));
        if (readClientConfig == null)
            throw new CosmosConfigurationException(
                $"Cannot find the Cosmos client for read operations for model {modelName}"
            );

        var writeClientConfig = cosmosConfiguration
            .CosmosClientConfigurations
            .FirstOrDefault(x => x.Name.Equals(
                modelConfig.WriteCosmosClientConfigurationName,
                StringComparison.CurrentCultureIgnoreCase
            ));
        if (writeClientConfig == null)
            throw new CosmosConfigurationException(
                $"Cannot find the Cosmos client for write operations for model {modelName}"
            );

        // Create the clients
        var clientBuilder = new CosmosClientBuilder(readClientConfig.ConnectionString);
        var cosmosReadClient = clientBuilder
            .WithThrottlingRetryOptions(
                maxRetryWaitTimeOnThrottledRequests: TimeSpan.FromSeconds(15),
                maxRetryAttemptsOnThrottledRequests: 10000
            )
            .WithBulkExecution(true)
            .Build();
        _readContainer = cosmosReadClient.GetContainer(
            modelConfig.DatabaseName,
            modelConfig.CollectionName
        );

        clientBuilder = new CosmosClientBuilder(writeClientConfig.ConnectionString);
        var cosmosWriteClient = clientBuilder
            .WithThrottlingRetryOptions(
                maxRetryWaitTimeOnThrottledRequests: TimeSpan.FromSeconds(15),
                maxRetryAttemptsOnThrottledRequests: 10000
            )
            .WithBulkExecution(true)
            .Build();
        _writeContainer = cosmosWriteClient.GetContainer(
            modelConfig.DatabaseName,
            modelConfig.CollectionName
        );
    }

    #region READ OPERATIONS

    /// <inheritdoc/>
    public async IAsyncEnumerable<T> GetAll(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // build the LINQ query with the soft-delete filter
        var linq = Queryable.Where(x => !((ICosmosDataModel)x).Deleted);

        // turn it into an async feed iterator
        using var feed = linq.ToFeedIterator();

        while (feed.HasMoreResults)
        {
            // await for the next page
            var page = await feed.ReadNextAsync(cancellationToken);

            foreach (var item in page)
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Streams up to <paramref name="count"/> items from the container, skipping the first
    /// <paramref name="offset"/> items and fetching in pages of <paramref name="limit"/> items.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to observe for cancellation of the async stream.
    /// </param>
    /// <param name="limit">
    /// The maximum number of items to request per page from the server.
    /// </param>
    /// <param name="offset">
    /// The number of items to skip before beginning to yield results.
    /// </param>
    /// <param name="count">
    /// The total number of items to yield before ending the stream.
    /// </param>
    /// <returns>
    /// An async‐stream of items matching the query, in ascending order, after applying offset and limit.
    /// </returns>
    /// <remarks>
    /// This implementation uses a SQL query with <c>OFFSET @offset LIMIT @limit</c> under the covers.  
    /// **Be aware** that Cosmos DB still needs to scan through all <c>offset + limit</c> items on the server,  
    /// charging you RUs for every row read (even those skipped). For deep pagination or large offsets,  
    /// prefer using continuation-token paging: it resumes exactly where the last page left off and only  
    /// incurs RUs for the items you actually read.  
    /// </remarks>
    public async IAsyncEnumerable<T> GetAll(
        [EnumeratorCancellation] CancellationToken cancellationToken,
        int limit,
        int offset,
        int count
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (count <= 0) yield break;

        // Build the SQL query with OFFSET/LIMIT, filtering out soft-deleted items
        const string sqlTemplate =
            "SELECT * FROM c WHERE c.isDeleted = false OFFSET @offset LIMIT @pageLimit";
        var queryDef = new QueryDefinition(sqlTemplate)
            .WithParameter("@offset", offset)
            .WithParameter("@pageLimit", limit);

        // Create the feed iterator with a max‐item‐count for each server‐side page
        var iterator = _readContainer.GetItemQueryIterator<T>(
            queryDef,
            requestOptions: new QueryRequestOptions { MaxItemCount = limit }
        );

        var remaining = count;
        while (iterator.HasMoreResults && remaining > 0)
        {
            // Real async call under the retry policy
            // var response = await _retryPolicy
            //     .ExecuteAsync(() => iterator.ReadNextAsync(cancellationToken));
            var response = await iterator.ReadNextAsync(cancellationToken);

            foreach (var item in response)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                yield return item;
                remaining--;

                if (remaining == 0)
                    yield break;
            }
        }
    }

    /// <inheritdoc/>
    public Task<T?> GetByIdAsync(string id)
    {
        var dao = _readContainer
            .GetItemLinqQueryable<T?>()
            .Where(x => ((ICosmosDataModel)x!).Id == id);

        if(!dao.Any())
            return Task.FromResult<T?>(null);
        
        if(dao.Count() > 1)
            throw new CosmosBaseException("Multiple records found for id: " + id);

        return Task.FromResult(dao.FirstOrDefault());
    }

    /// <inheritdoc/>
    public async Task<T?> GetByIdAsync(string id, string partitionKey)
    {
        try
        {
            // Convert partition key value to PartitionKey object
            PartitionKey pk = new PartitionKey(partitionKey);

            // Read the item from the container
            ItemResponse<T> response = await _readContainer.ReadItemAsync<T>(id, pk);

            // Return the item if found
            return response.Resource;
        }
        catch (CosmosException)
        {
            // Handle the case where the item does not exist
            return null;
        }
    }

    #endregion

    #region WRITE OPERATIONS

    /// <inheritdoc/>
    public async Task<T> AddAsync(ICosmosDataModel document)
    {
        if (document is null)
            throw new CosmosBaseException("Cannot add a model that is null");

        await _writeContainer.CreateItemAsync(document);
        return (T)document;
    }

    /// <inheritdoc/>
    public async Task<T> CreateAsync(ICosmosDataModel document)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.CreatedOnUtc = DateTime.UtcNow;
        document.UpdatedOnUtc = DateTime.UtcNow;
        document.Deleted = false;

        var partitionKey = document
            .GetType()
            .GetProperty(_partitionKeyProperty)
            ?.GetValue(document, null);

        await _writeContainer.CreateItemAsync(
            (T)document,
            new PartitionKey(partitionKey?.ToString())
        );

        return (T)document;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string id, DeleteOptions deleteOptions)
    {
        switch (deleteOptions)
        {
            case DeleteOptions.HardDelete:
                await _writeContainer.DeleteItemAsync<T>(id, new PartitionKey(_partitionKeyProperty));
                return;
            case DeleteOptions.SoftDelete:
                var item = await GetByIdAsync(id);
                if (item is null)
                    return;
                ((ICosmosDataModel)item).Deleted = true;
                await UpdateAsync((ICosmosDataModel)item);
                return;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string id, string partitionKey, DeleteOptions deleteOptions)
    {
        switch (deleteOptions)
        {
            case DeleteOptions.HardDelete:
                await _writeContainer.DeleteItemAsync<T>(id, new PartitionKey(partitionKey));
                return;
            case DeleteOptions.SoftDelete:
                var item = await GetByIdAsync(id, partitionKey);
                if (item is null)
                    return;
                ((ICosmosDataModel)item).Deleted = true;
                await UpdateAsync((ICosmosDataModel)item);
                return;
        }
    }

    /// <inheritdoc/>
    public async Task<T> UpdateAsync(ICosmosDataModel document)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.UpdatedOnUtc = DateTime.UtcNow;

        var partitionKey = document
            .GetType()
            .GetProperty(_partitionKeyProperty)
            ?.GetValue(document, null);

        var response = await _writeContainer.UpsertItemAsync(
            (T)document,
            new PartitionKey(partitionKey?.ToString())
        );

        return response;
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetAllByPropertyAsync(string propertyName, string propertyValue)
    {
        var queryString =
            $"SELECT * FROM c WHERE c.{propertyName} = @propertyValue AND c.IsDeleted = false";

        // Create query definition with parameters
        var queryDefinition = new QueryDefinition(queryString).WithParameter(
            "@propertyValue",
            propertyValue
        );

        var queryResultSetIterator = _readContainer.GetItemQueryIterator<T>(queryDefinition);

        var results = new List<T>();

        // Iterate through the result set
        while (queryResultSetIterator.HasMoreResults)
        {
            var response = await queryResultSetIterator.ReadNextAsync();
            results.AddRange(response.ToList());
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetAllByArrayPropertyAsync(
        string arrayName,
        string arrayPropertyName,
        string propertyValue
    )
    {
        // Parameter for the root document (T is the document type)
        var parameter = Expression.Parameter(typeof(T), "item");

        // Access the array by its name (e.g., item.AccountMembers)
        var arrayProperty = Expression.PropertyOrField(parameter, arrayName);

        // Get the array element type dynamically
        var arrayElementType = typeof(T)
            .GetProperty(arrayName)
            ?.PropertyType.GetGenericArguments()[0];

        if (arrayElementType == null)
        {
            throw new InvalidOperationException(
                $"Could not determine the element type of the array '{arrayName}'"
            );
        }

        // Create a parameter for each array element
        var arrayItemParameter = Expression.Parameter(arrayElementType, "arrayItem");

        // Access the property inside the array element (e.g., arrayItem.Email)
        var arrayPropertyAccess = Expression.PropertyOrField(arrayItemParameter, arrayPropertyName);

        // Create an expression to compare the property value (e.g., arrayItem.Email == "test@example.com")
        var equalityExpression = Expression.Equal(
            arrayPropertyAccess,
            Expression.Constant(propertyValue)
        );

        // Create a lambda expression for filtering array elements
        var lambdaForAny = Expression.Lambda(equalityExpression, arrayItemParameter);

        // Use the Any() method to filter the array (e.g., item.AccountMembers.Any(arrayItem => arrayItem.Email == "test@example.com"))
        var anyMethod = typeof(Enumerable)
            .GetMethods()
            .First(m => m.Name == "Any" && m.GetParameters().Length == 2)
            .MakeGenericMethod(arrayElementType);

        var anyCall = Expression.Call(anyMethod, arrayProperty, lambdaForAny);

        // Create the final lambda expression for the queryable (e.g., item => item.AccountMembers.Any(...))
        var lambdaForWhere = Expression.Lambda<Func<T, bool>>(anyCall, parameter);

        // Apply the Where clause dynamically to the queryable
        var query = Queryable.Where(lambdaForWhere).ToFeedIterator();

        var results = new List<T>();

        // Execute the query and gather results
        while (query.HasMoreResults)
        {
            foreach (var result in await query.ReadNextAsync())
            {
                results.Add(result);
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T> GetAll(
        [EnumeratorCancellation] CancellationToken cancellationToken,
        string partitionKey
    )
    {
        var sqlQueryText =
            $"SELECT * FROM c WHERE c.{_partitionKeyProperty} = @partitionKey AND c.IsDeleted = false";

        QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText).WithParameter(
            "@partitionKey",
            partitionKey
        );

        using FeedIterator<T> queryResultSetIterator = _readContainer.GetItemQueryIterator<T>(
            queryDefinition
        );
        while (queryResultSetIterator.HasMoreResults)
        {
            FeedResponse<T> currentResultSet = await queryResultSetIterator.ReadNextAsync(cancellationToken);
            foreach (T item in currentResultSet)
            {
                yield return item;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetAllByPropertyComparisonAsync(List<PropertyFilter> propertyFilters)
    {
        // Start building the query string
        string queryString = "SELECT * FROM c WHERE ";

        // Dynamically construct the WHERE clause
        // EXCEPT THE IN CLAUSES
        var conditions = propertyFilters.Select(p =>
        {
            if (p.PropertyComparison == PropertyComparison.In)
            {
                // If the list is empty we do not want to add the IN clause
                var itemList = (List<string>)p.PropertyValue;
                if (itemList.Count == 0)
                    return string.Empty;

                var propertyValueList = (List<string>)p.PropertyValue;
                var itemListQueryInPortion = string.Join(
                    ", ",
                    propertyValueList.Select(v => $"\"{v}\"")
                );
                return $"c.{p.PropertyName[1..]} IN ({itemListQueryInPortion})";
            }
            else
            {
                return $"c.{p.PropertyName[1..]} {p.PropertyComparison} {p.PropertyName}";
            }
        });

        queryString += string.Join(" AND ", conditions.Where(c => !string.IsNullOrEmpty(c)));

        // Create the QueryDefinition
        var queryDefinition = new QueryDefinition(queryString);

        // Add the parameters dynamically
        foreach (var param in propertyFilters)
        {
            if (param.PropertyComparison != PropertyComparison.In)
            {
                queryDefinition = queryDefinition.WithParameter(
                    param.PropertyName,
                    param.PropertyValue
                );
            }
        }

        var queryResultSetIterator = _readContainer.GetItemQueryIterator<T>(queryDefinition);

        var results = new List<T>();

        // Iterate through the result set
        while (queryResultSetIterator.HasMoreResults)
        {
            var response = await queryResultSetIterator.ReadNextAsync();
            results.AddRange(response.ToList());
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<IList<T>> GetAllDistinctInListByPropertyAsync(
        CancellationToken cancellationToken,
        string propertyName,
        List<string> propertyValueList
    )
    {
        var queryString =
            $"SELECT DISTINCT c.{propertyName} FROM c WHERE c.{propertyName} IN ({string.Join(",", propertyValueList.Select(s => $"\"{s}\""))}) AND c.IsDeleted = false";

        // Create query definition with parameters
        var queryDefinition = new QueryDefinition(queryString);

        var queryResultSetIterator = _readContainer.GetItemQueryIterator<T>(queryDefinition);

        var results = new List<T>();

        // Iterate through the result set
        while (queryResultSetIterator.HasMoreResults)
        {
            var response = await queryResultSetIterator.ReadNextAsync(cancellationToken);
            results.AddRange(response.ToList());
        }

        return results;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T> GetByQuery(
        [EnumeratorCancellation] CancellationToken cancellationToken,
        string query
    )
    {
        // Create query definition with parameters
        var queryDefinition = new QueryDefinition(query);

        var queryResultSetIterator = _readContainer.GetItemQueryIterator<T>(queryDefinition);

        // Iterate through the result set
        while (queryResultSetIterator.HasMoreResults)
        {
            var response = await queryResultSetIterator.ReadNextAsync(cancellationToken);
            foreach (var item in response.ToList())
                yield return item;
        }
    }

    #endregion
}