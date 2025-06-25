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

/// <summary>
/// Repositories are only concerned with DAOs (Data Access Objects)
/// and not DTOs (Data Transfer Objects)
///
/// Do NOT mix the two
///
/// </summary>
/// <typeparam name="T"></typeparam>
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
    public async IAsyncEnumerable<T> GetAll(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        foreach (T item in Queryable.Where(x => ((ICosmosDataModel)x).IsDeleted == false))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            yield return item;
        }
    }

    public IAsyncEnumerable<T> GetAll(
        CancellationToken cancellationToken,
        int limit,
        int offset,
        int count
    )
    {
        throw new NotImplementedException();
    }

    public Task<T?> GetByIdAsync(string id)
    {
        //var dao = Queryable.Where(x => ((ICosmosDataModel)x).Id == id).AsEnumerable();
        var dao = _readContainer
            .GetItemLinqQueryable<T?>()
            .FirstOrDefault(x => ((ICosmosDataModel)x!).Id == id);

        // TODO - If we have more than one match we should probably throw an exception
        // Do this here

        return Task.FromResult(dao);
    }

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
    public async Task<T> AddAsync(ICosmosDataModel document)
    {
        if (document is null)
            throw new CosmosBaseException("Cannot add a model that is null");

        await _writeContainer.CreateItemAsync(document);
        return (T)document;
    }

    public async Task<T> CreateAsync(ICosmosDataModel document)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.CreatedDateTimeUtc = DateTime.UtcNow;
        document.UpdatedDateTimeUtc = DateTime.UtcNow;
        document.IsDeleted = false;

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
                ((ICosmosDataModel)item).IsDeleted = true;
                await UpdateAsync((ICosmosDataModel)item);
                return;
        }
    }

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
                ((ICosmosDataModel)item).IsDeleted = true;
                await UpdateAsync((ICosmosDataModel)item);
                return;
        }
    }

    public async Task<T> UpdateAsync(ICosmosDataModel document)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.UpdatedDateTimeUtc = DateTime.UtcNow;

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

    /// <summary>
    /// Search for a value of a member in an array of objects
    /// </summary>
    /// <param name="arrayName"></param>
    /// <param name="arrayPropertyName"></param>
    /// <param name="propertyValue"></param>
    /// <returns></returns>
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