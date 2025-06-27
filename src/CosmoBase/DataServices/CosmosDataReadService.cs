using System.Runtime.CompilerServices;
using CosmoBase.Abstractions.Exceptions;
using CosmoBase.Abstractions.Filters;
using CosmoBase.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace CosmoBase.DataServices;

/// <summary>
/// High-performance implementation of <see cref="ICosmosDataReadService{T}"/> that provides comprehensive
/// read operations with automatic DTO/DAO mapping, intelligent caching, and enterprise-grade error handling.
/// </summary>
/// <typeparam name="TDto">The DTO type exposed to consumers that represents the domain model.</typeparam>
/// <typeparam name="TDao">The DAO type stored in Cosmos DB that implements <see cref="ICosmosDataModel"/>.</typeparam>
public class CosmosDataReadService<TDto, TDao> : ICosmosDataReadService<TDto>
    where TDao : class, ICosmosDataModel, new()
    where TDto : class, new()
{
    private readonly ICosmosRepository<TDao> _cosmosRepository;
    private readonly IItemMapper<TDao, TDto> _mapper;
    private readonly ILogger<CosmosDataReadService<TDto, TDao>>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDataReadService{TDto,TDao}"/> class.
    /// </summary>
    /// <param name="cosmosRepository">The repository for low-level Cosmos DB operations.</param>
    /// <param name="mapper">The mapper for converting between DAO and DTO types.</param>
    /// <param name="logger">Optional logger for operation tracking and debugging.</param>
    public CosmosDataReadService(
        ICosmosRepository<TDao> cosmosRepository,
        IItemMapper<TDao, TDto> mapper,
        ILogger<CosmosDataReadService<TDto, TDao>>? logger = null)
    {
        _cosmosRepository = cosmosRepository ?? throw new ArgumentNullException(nameof(cosmosRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TDto?> GetByIdAsync(string id, string partitionKey, bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentNullException(nameof(id));
        if (string.IsNullOrWhiteSpace(partitionKey))
            throw new ArgumentNullException(nameof(partitionKey));

        try
        {
            _logger?.LogDebug("Retrieving document {DocumentId} from partition {PartitionKey}", id, partitionKey);

            var dao = await _cosmosRepository.GetItemAsync(id, partitionKey, includeDeleted, cancellationToken);

            if (dao == null)
            {
                _logger?.LogDebug("Document {DocumentId} not found", id);
                return null;
            }

            var result = _mapper.FromDao(dao);
            _logger?.LogInformation("Successfully retrieved document {DocumentId}", id);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve document {DocumentId}", id);
            throw new CosmoBaseException($"Failed to retrieve document {id}: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<List<TDto>> GetAllByArrayPropertyAsync(
        string arrayName,
        string elementPropertyName,
        object elementPropertyValue,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(arrayName))
            throw new ArgumentNullException(nameof(arrayName));
        if (string.IsNullOrWhiteSpace(elementPropertyName))
            throw new ArgumentNullException(nameof(elementPropertyName));
        if (elementPropertyValue == null)
            throw new ArgumentNullException(nameof(elementPropertyValue));

        try
        {
            _logger?.LogDebug("Querying by array property {ArrayName}.{ElementPropertyName} = {ElementPropertyValue}",
                arrayName, elementPropertyName, elementPropertyValue);

            var daos = await _cosmosRepository.GetAllByArrayPropertyAsync(
                arrayName,
                elementPropertyName,
                elementPropertyValue,
                includeDeleted,
                cancellationToken);

            var results = _mapper.FromDaos(daos).ToList();
            _logger?.LogInformation("Array property query returned {ResultCount} documents", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to query by array property {ArrayName}.{ElementPropertyName}",
                arrayName, elementPropertyName);
            throw new CosmoBaseException($"Array property query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<List<TDto>> GetAllByPropertyComparisonAsync(
        IEnumerable<PropertyFilter> propertyFilters,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        if (propertyFilters == null)
            throw new ArgumentNullException(nameof(propertyFilters));

        var filters = propertyFilters.ToList();

        try
        {
            _logger?.LogDebug("Querying by {FilterCount} property filters", filters.Count);

            var daos = await _cosmosRepository.GetAllByPropertyComparisonAsync(filters, includeDeleted,
                cancellationToken);
            var results = _mapper.FromDaos(daos).ToList();

            _logger?.LogInformation("Property filter query returned {ResultCount} documents", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to query by property comparison");
            throw new CosmoBaseException($"Property comparison query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TDto> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Starting cross-partition streaming query");

        try
        {
            return _mapper.FromDaosAsync(_cosmosRepository.GetAllAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize cross-partition streaming query");
            throw new CosmoBaseException($"Cross-partition streaming query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TDto> GetAllAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partitionKey))
            throw new ArgumentNullException(nameof(partitionKey));

        _logger?.LogDebug("Starting single-partition streaming query for partition {PartitionKey}", partitionKey);

        try
        {
            return _mapper.FromDaosAsync(_cosmosRepository.GetAllAsync(partitionKey, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize single-partition streaming query");
            throw new CosmoBaseException($"Single-partition streaming query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TDto> GetAllAsync(int limit, int offset, int count,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than 0");
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative");

        _logger?.LogDebug("Starting offset-based streaming query (limit: {Limit}, offset: {Offset}, count: {Count})",
            limit, offset, count);

        try
        {
            return _mapper.FromDaosAsync(_cosmosRepository.GetAllAsync(limit, offset, count, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize offset-based streaming query");
            throw new CosmoBaseException($"Offset-based streaming query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TDto> QueryAsync(ISpecification<TDto> specification,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
            throw new ArgumentNullException(nameof(specification));

        if (specification is not SqlSpecification<TDto> sqlSpecification)
        {
            throw new ArgumentException("Specification must be a SqlSpecification", nameof(specification));
        }

        try
        {
            _logger?.LogDebug("Starting specification-based streaming query");

            var daoSpecification = new SqlSpecification<TDao>(
                sqlSpecification.QueryText,
                sqlSpecification.Parameters?.ToDictionary());

            return _mapper.FromDaosAsync(_cosmosRepository.QueryAsync(daoSpecification, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize specification-based streaming query");
            throw new CosmoBaseException($"Specification-based streaming query failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<List<TDto>> BulkReadAsyncEnumerable(
        ISpecification<TDto> specification,
        string partitionKey,
        int batchSize = 100,
        int maxConcurrency = 50,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (specification == null)
            throw new ArgumentNullException(nameof(specification));
        if (string.IsNullOrWhiteSpace(partitionKey))
            throw new ArgumentNullException(nameof(partitionKey));
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (maxConcurrency < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

        if (specification is not SqlSpecification<TDto> sqlSpecification)
        {
            throw new ArgumentException("Specification must be a SqlSpecification", nameof(specification));
        }

        _logger?.LogInformation("Starting bulk read operation for partition {PartitionKey}", partitionKey);

        var daoSpecification = new SqlSpecification<TDao>(
            sqlSpecification.QueryText,
            sqlSpecification.Parameters?.ToDictionary());

        var totalBatches = 0;
        var totalDocuments = 0;

        IAsyncEnumerable<List<TDao>> daoAsyncEnumerable;

        try
        {
            daoAsyncEnumerable = _cosmosRepository.BulkReadAsyncEnumerable(
                daoSpecification,
                partitionKey,
                batchSize,
                maxConcurrency,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize bulk read operation");
            throw new CosmoBaseException($"Bulk read operation failed: {ex.Message}", ex);
        }

        await foreach (var daoBatch in daoAsyncEnumerable.WithCancellation(cancellationToken))
        {
            List<TDto> dtoBatch;
            try
            {
                dtoBatch = _mapper.FromDaos(daoBatch).ToList();
                totalBatches++;
                totalDocuments += dtoBatch.Count;

                _logger?.LogDebug("Processed batch {BatchNumber} with {DocumentCount} documents",
                    totalBatches, dtoBatch.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to process batch {BatchNumber}", totalBatches + 1);
                throw new CosmoBaseException($"Failed to process batch: {ex.Message}", ex);
            }

            yield return dtoBatch;
        }

        _logger?.LogInformation("Completed bulk read: {TotalBatches} batches, {TotalDocuments} documents",
            totalBatches, totalDocuments);
    }

    /// <inheritdoc />
    public async Task<int> GetCountAsync(string partitionKeyValue, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partitionKeyValue))
            throw new ArgumentNullException(nameof(partitionKeyValue));

        try
        {
            _logger?.LogDebug("Getting document count for partition {PartitionKey}", partitionKeyValue);

            var count = await _cosmosRepository.GetCountAsync(partitionKeyValue, cancellationToken);

            _logger?.LogInformation("Document count for partition {PartitionKey}: {Count}", partitionKeyValue, count);
            return count;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get document count");
            throw new CosmoBaseException($"Count operation failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<int> GetTotalCountAsync(string partitionKeyValue, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partitionKeyValue))
            throw new ArgumentNullException(nameof(partitionKeyValue));

        try
        {
            _logger?.LogDebug("Getting total document count for partition {PartitionKey}", partitionKeyValue);

            var totalCount = await _cosmosRepository.GetTotalCountAsync(partitionKeyValue, cancellationToken);

            _logger?.LogInformation("Total document count for partition {PartitionKey}: {TotalCount}",
                partitionKeyValue, totalCount);
            return totalCount;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get total document count");
            throw new CosmoBaseException($"Total count operation failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<int> GetCountWithCacheAsync(
        string partitionKeyValue,
        int cacheExpiryMinutes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partitionKeyValue))
            throw new ArgumentNullException(nameof(partitionKeyValue));
        if (cacheExpiryMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(cacheExpiryMinutes));

        try
        {
            _logger?.LogDebug("Getting cached document count for partition {PartitionKey}", partitionKeyValue);

            var count = await _cosmosRepository.GetCountWithCacheAsync(
                partitionKeyValue,
                cacheExpiryMinutes,
                cancellationToken);

            _logger?.LogInformation("Cached document count for partition {PartitionKey}: {Count}",
                partitionKeyValue, count);
            return count;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get cached document count");
            throw new CosmoBaseException($"Cached count operation failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public void InvalidateCountCache(string partitionKeyValue)
    {
        if (string.IsNullOrWhiteSpace(partitionKeyValue))
            throw new ArgumentNullException(nameof(partitionKeyValue));

        try
        {
            _logger?.LogDebug("Invalidating count cache for partition {PartitionKey}", partitionKeyValue);

            _cosmosRepository.InvalidateCountCache(partitionKeyValue);

            _logger?.LogInformation("Successfully invalidated count cache for partition {PartitionKey}",
                partitionKeyValue);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to invalidate count cache");
            throw new CosmoBaseException($"Cache invalidation failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<(IList<TDto> Items, string? ContinuationToken)> GetPageWithTokenAsync(
        ISpecification<TDto> specification,
        string partitionKey,
        int pageSize,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
            throw new ArgumentNullException(nameof(specification));
        if (string.IsNullOrWhiteSpace(partitionKey))
            throw new ArgumentNullException(nameof(partitionKey));
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize));

        if (specification is not SqlSpecification<TDto> sqlSpecification)
        {
            throw new ArgumentException("Specification must be a SqlSpecification", nameof(specification));
        }

        try
        {
            _logger?.LogDebug("Getting page for partition {PartitionKey}, pageSize: {PageSize}",
                partitionKey, pageSize);

            var daoSpecification = new SqlSpecification<TDao>(
                sqlSpecification.QueryText,
                sqlSpecification.Parameters?.ToDictionary());

            var (daoItems, token) = await _cosmosRepository.GetPageWithTokenAsync(
                daoSpecification,
                partitionKey,
                pageSize,
                continuationToken,
                cancellationToken);

            var dtoItems = _mapper.FromDaos(daoItems).ToList();

            _logger?.LogInformation("Page retrieved: {ItemCount} items, hasNextPage: {HasNextPage}",
                dtoItems.Count, token != null);

            return (dtoItems, token);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get page with token");
            throw new CosmoBaseException($"Pagination operation failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<(IList<TDto> Items, string? ContinuationToken, int? TotalCount)> GetPageWithTokenAndCountAsync(
        ISpecification<TDto> specification,
        string partitionKey,
        int pageSize,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null)
            throw new ArgumentNullException(nameof(specification));
        if (string.IsNullOrWhiteSpace(partitionKey))
            throw new ArgumentNullException(nameof(partitionKey));
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize));

        if (specification is not SqlSpecification<TDto> sqlSpecification)
        {
            throw new ArgumentException("Specification must be a SqlSpecification", nameof(specification));
        }

        try
        {
            var isFirstPage = continuationToken == null;
            _logger?.LogDebug("Getting page with count for partition {PartitionKey}, isFirstPage: {IsFirstPage}",
                partitionKey, isFirstPage);

            var daoSpecification = new SqlSpecification<TDao>(
                sqlSpecification.QueryText,
                sqlSpecification.Parameters?.ToDictionary());

            var (daoItems, token, totalCount) = await _cosmosRepository.GetPageWithTokenAndCountAsync(
                daoSpecification,
                partitionKey,
                pageSize,
                continuationToken,
                cancellationToken);

            var dtoItems = _mapper.FromDaos(daoItems).ToList();

            _logger?.LogInformation("Page with count retrieved: {ItemCount} items, totalCount: {TotalCount}",
                dtoItems.Count, totalCount);

            return (dtoItems, token, totalCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get page with token and count");
            throw new CosmoBaseException($"Pagination with count operation failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    Task<TDto?> IDataReadService<TDto, string>.GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        _logger?.LogWarning("Attempted to call unsupported GetByIdAsync method with ID only: {DocumentId}", id);
        throw new CosmoBaseException(
            $"Cosmos DB operations require both document ID and partition key. " +
            $"Use GetByIdAsync(string id, string partitionKey, bool includeDeleted) instead. " +
            $"Document ID: {id}");
    }

    /// <inheritdoc />
    IAsyncEnumerable<TDto> IDataReadService<TDto, string>.GetAllAsync(CancellationToken cancellationToken)
    {
        return GetAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    IAsyncEnumerable<TDto> IDataReadService<TDto, string>.QueryAsync(ISpecification<TDto> specification,
        CancellationToken cancellationToken)
    {
        return QueryAsync(specification, cancellationToken);
    }
}