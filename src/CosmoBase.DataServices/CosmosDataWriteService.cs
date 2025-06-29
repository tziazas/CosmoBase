using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Exceptions;
using CosmoBase.Abstractions.Filters;
using CosmoBase.Abstractions.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PatchOperationType = CosmoBase.Abstractions.Enums.PatchOperationType;

namespace CosmoBase.DataServices;

/// <summary>
/// High-performance implementation of <see cref="ICosmosDataWriteService{TDto, TDao}"/> that provides comprehensive
/// write operations with automatic DTO/DAO mapping, audit field management, and enterprise-grade error handling.
/// </summary>
/// <typeparam name="TDto">The DTO type used by application code and exposed through the service interface.</typeparam>
/// <typeparam name="TDao">The DAO type stored in Cosmos DB that implements <see cref="ICosmosDataModel"/>.</typeparam>
/// <remarks>
/// This service acts as a translation layer between domain objects (DTOs) and persistence objects (DAOs),
/// providing automatic mapping, validation, and audit field management. All operations include comprehensive
/// error handling, logging, and performance optimizations for production workloads.
/// </remarks>
public class CosmosDataWriteService<TDto, TDao> : ICosmosDataWriteService<TDto, TDao>
    where TDto : class
    where TDao : class, ICosmosDataModel
{
    private readonly ICosmosRepository<TDao> _cosmosRepository;
    private readonly IItemMapper<TDao, TDto> _mapper;
    private readonly ILogger<CosmosDataWriteService<TDto, TDao>>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDataWriteService{TDto,TDao}"/> class.
    /// </summary>
    /// <param name="cosmosRepository">The repository for low-level Cosmos DB operations.</param>
    /// <param name="mapper">The mapper for converting between DTO and DAO types.</param>
    /// <param name="logger">Optional logger for operation tracking and debugging.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="cosmosRepository"/> or <paramref name="mapper"/> is null.
    /// </exception>
    public CosmosDataWriteService(
        ICosmosRepository<TDao> cosmosRepository,
        IItemMapper<TDao, TDto> mapper,
        ILogger<CosmosDataWriteService<TDto, TDao>>? logger = null)
    {
        _cosmosRepository = cosmosRepository ?? throw new ArgumentNullException(nameof(cosmosRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger;
    }

    #region Single Document Operations

    /// <inheritdoc />
    public async Task<TDto> CreateAsync(
        TDto entity,
        CancellationToken cancellationToken = default)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        try
        {
            _logger?.LogDebug("Creating new document of type {DtoType}", typeof(TDto).Name);

            var dao = _mapper.ToDao(entity);
            var created = await _cosmosRepository.CreateItemAsync(dao, cancellationToken);
            var result = _mapper.FromDao(created);

            _logger?.LogInformation("Successfully created document with ID {DocumentId}", created.Id);
            return result;
        }
        catch (CosmosException ex)
        {
            _logger?.LogError(ex, "Failed to create document of type {DtoType}: {StatusCode} - {Message}",
                typeof(TDto).Name, ex.StatusCode, ex.Message);
            throw new CosmoBaseException($"Failed to create document: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error creating document of type {DtoType}", typeof(TDto).Name);
            throw new CosmoBaseException($"Unexpected error during document creation: {ex.Message}", ex);
        }
    }

    Task<TDto?> IDataWriteService<TDto, string>.CreateAsync(TDto entity,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "This operation is not implemented for this type. " +
            "Use ICosmosDataWriteService<TDto, TDao>.CreateAsync instead.");
    }

    /// <inheritdoc />
    public async Task<TDto> ReplaceAsync(
        TDto entity,
        CancellationToken cancellationToken = default)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        try
        {
            _logger?.LogDebug("Replacing document of type {DtoType}", typeof(TDto).Name);

            var dao = _mapper.ToDao(entity);
            var replaced = await _cosmosRepository.ReplaceItemAsync(dao, cancellationToken);
            var result = _mapper.FromDao(replaced);

            _logger?.LogInformation("Successfully replaced document with ID {DocumentId}", replaced.Id);
            return result;
        }
        catch (CosmosException ex)
        {
            _logger?.LogError(ex, "Failed to replace document of type {DtoType}: {StatusCode} - {Message}",
                typeof(TDto).Name, ex.StatusCode, ex.Message);
            throw new CosmoBaseException($"Failed to replace document: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error replacing document of type {DtoType}", typeof(TDto).Name);
            throw new CosmoBaseException($"Unexpected error during document replacement: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TDto> UpsertAsync(
        TDto entity,
        CancellationToken cancellationToken = default)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        try
        {
            _logger?.LogDebug("Upserting document of type {DtoType}", typeof(TDto).Name);

            var dao = _mapper.ToDao(entity);
            var upserted = await _cosmosRepository.UpsertItemAsync(dao, cancellationToken);
            var result = _mapper.FromDao(upserted);

            _logger?.LogInformation("Successfully upserted document with ID {DocumentId}", upserted.Id);
            return result;
        }
        catch (CosmosException ex)
        {
            _logger?.LogError(ex, "Failed to upsert document of type {DtoType}: {StatusCode} - {Message}",
                typeof(TDto).Name, ex.StatusCode, ex.Message);
            throw new CosmoBaseException($"Failed to upsert document: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error upserting document of type {DtoType}", typeof(TDto).Name);
            throw new CosmoBaseException($"Unexpected error during document upsert: {ex.Message}", ex);
        }
    }

    #endregion

    #region Bulk Operations

    /// <inheritdoc />
    public async Task BulkUpsertAsync(
        IEnumerable<TDto> items,
        Func<TDto, string> partitionKeySelector,
        Action<TDto>? configureItem = null,
        int batchSize = 100,
        int maxConcurrency = 10,
        CancellationToken cancellationToken = default)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (partitionKeySelector == null)
            throw new ArgumentNullException(nameof(partitionKeySelector));

        // Materialize the incoming enumerable only once to avoid multiple enumeration
        var dtoList = items.ToList();

        if (!dtoList.Any())
        {
            _logger?.LogDebug("BulkUpsertAsync called with empty collection, skipping operation");
            return;
        }

        try
        {
            _logger?.LogInformation("Starting bulk upsert operation for {ItemCount} items of type {DtoType}",
                dtoList.Count, typeof(TDto).Name);

            // Extract and validate partition keys
            var partitionKeys = dtoList.Select(partitionKeySelector).Distinct().ToList();
            if (partitionKeys.Count != 1)
            {
                throw new CosmoBaseException(
                    $"All items in a bulk operation must belong to the same partition. Found {partitionKeys.Count} distinct partition keys: {string.Join(", ", partitionKeys)}");
            }

            var partitionKey = partitionKeys.Single();

            // Apply configuration and map to DAOs
            var daoItems = dtoList
                .Select(dto =>
                {
                    configureItem?.Invoke(dto);
                    return _mapper.ToDao(dto);
                })
                .ToList();

            // Execute bulk upsert
            await _cosmosRepository.BulkUpsertAsync(
                daoItems,
                partitionKey,
                batchSize,
                maxConcurrency,
                cancellationToken);

            _logger?.LogInformation("Successfully completed bulk upsert operation for {ItemCount} items",
                dtoList.Count);
        }
        catch (CosmoBaseException ex) when (ex.Data.Contains("BulkUpsertResult"))
        {
            // Re-throw bulk-specific exceptions with additional context
            _logger?.LogError(ex, "Bulk upsert operation failed for {DtoType}", typeof(TDto).Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during bulk upsert operation for type {DtoType}",
                typeof(TDto).Name);
            throw new CosmoBaseException($"Bulk upsert operation failed: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task BulkInsertAsync(
        IEnumerable<TDto> items,
        Func<TDto, string> partitionKeySelector,
        Action<TDto>? configureItem = null,
        int batchSize = 100,
        int maxConcurrency = 10,
        CancellationToken cancellationToken = default)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (partitionKeySelector == null)
            throw new ArgumentNullException(nameof(partitionKeySelector));

        // Materialize the incoming enumerable only once to avoid multiple enumeration
        var dtoList = items.ToList();

        if (!dtoList.Any())
        {
            _logger?.LogDebug("BulkInsertAsync called with empty collection, skipping operation");
            return;
        }

        try
        {
            _logger?.LogInformation("Starting bulk insert operation for {ItemCount} items of type {DtoType}",
                dtoList.Count, typeof(TDto).Name);

            // Extract and validate partition keys
            var partitionKeys = dtoList.Select(partitionKeySelector).Distinct().ToList();
            if (partitionKeys.Count != 1)
            {
                throw new CosmoBaseException(
                    $"All items in a bulk operation must belong to the same partition. Found {partitionKeys.Count} distinct partition keys: {string.Join(", ", partitionKeys)}");
            }

            var partitionKey = partitionKeys.Single();

            // Apply configuration and map to DAOs
            var daoItems = dtoList
                .Select(dto =>
                {
                    configureItem?.Invoke(dto);
                    return _mapper.ToDao(dto);
                })
                .ToList();

            // Execute bulk insert
            await _cosmosRepository.BulkInsertAsync(
                daoItems,
                partitionKey,
                batchSize,
                maxConcurrency,
                cancellationToken);

            _logger?.LogInformation("Successfully completed bulk insert operation for {ItemCount} items",
                dtoList.Count);
        }
        catch (CosmoBaseException ex) when (ex.Data.Contains("BulkInsertResult"))
        {
            // Re-throw bulk-specific exceptions with additional context
            _logger?.LogError(ex, "Bulk insert operation failed for {DtoType}", typeof(TDto).Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during bulk insert operation for type {DtoType}",
                typeof(TDto).Name);
            throw new CosmoBaseException($"Bulk insert operation failed: {ex.Message}", ex);
        }
    }

    #endregion

    #region Patch Operations

    /// <inheritdoc />
    public async Task<TDto?> PatchDocumentAsync(
        string id,
        string partitionKey,
        PatchSpecification patchSpec,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentNullException(nameof(id));
        if (string.IsNullOrWhiteSpace(partitionKey))
            throw new ArgumentNullException(nameof(partitionKey));
        ArgumentNullException.ThrowIfNull(patchSpec);

        try
        {
            _logger?.LogDebug(
                "Patching document {DocumentId} in partition {PartitionKey} with {OperationCount} operations",
                id, partitionKey, patchSpec.Operations.Count);

            var patchedDao = await _cosmosRepository.PatchItemAsync(id, partitionKey, patchSpec, cancellationToken);

            if (patchedDao == null)
            {
                _logger?.LogWarning(
                    "Patch operation returned null for document {DocumentId} in partition {PartitionKey}",
                    id, partitionKey);
                return null;
            }

            var result = _mapper.FromDao(patchedDao);
            _logger?.LogInformation("Successfully patched document {DocumentId}", id);
            return result;
        }
        catch (CosmosException ex)
        {
            _logger?.LogError(ex, "Failed to patch document {DocumentId}: {StatusCode} - {Message}",
                id, ex.StatusCode, ex.Message);
            throw new CosmoBaseException($"Failed to patch document {id}: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error patching document {DocumentId}", id);
            throw new CosmoBaseException($"Unexpected error during document patch: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TDto?> PatchDocumentListItemAsync(
        string id,
        string partitionKey,
        string listPropertyName,
        string listItemId,
        string parameterName,
        object replacementValue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentNullException(nameof(id));
        if (string.IsNullOrWhiteSpace(partitionKey))
            throw new ArgumentNullException(nameof(partitionKey));
        if (string.IsNullOrWhiteSpace(listPropertyName))
            throw new ArgumentNullException(nameof(listPropertyName));
        if (string.IsNullOrWhiteSpace(listItemId))
            throw new ArgumentNullException(nameof(listItemId));
        if (string.IsNullOrWhiteSpace(parameterName))
            throw new ArgumentNullException(nameof(parameterName));
        if (replacementValue == null)
            throw new ArgumentNullException(nameof(replacementValue));

        try
        {
            _logger?.LogDebug("Patching list item {ListItemId} in property {ListPropertyName} of document {DocumentId}",
                listItemId, listPropertyName, id);

            // Build JSON Path expression for the array element
            var path = $"{listPropertyName}[?(@.id=='{listItemId}')].{parameterName}";
            var patchOperation = new PatchOperationSpecification(path, PatchOperationType.Replace, replacementValue);
            var patchSpec = new PatchSpecification([patchOperation]);

            var result = await PatchDocumentAsync(id, partitionKey, patchSpec, cancellationToken);

            if (result != null)
            {
                _logger?.LogInformation("Successfully patched list item {ListItemId} in document {DocumentId}",
                    listItemId, id);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to patch list item {ListItemId} in document {DocumentId}", listItemId, id);
            throw;
        }
    }

    #endregion

    #region Delete Operations

    /// <inheritdoc />
    public async Task DeleteAsync(
        string id,
        string partitionKey,
        DeleteOptions deleteOptions,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentNullException(nameof(id));
        if (string.IsNullOrWhiteSpace(partitionKey))
            throw new ArgumentNullException(nameof(partitionKey));

        try
        {
            _logger?.LogDebug("Deleting document {DocumentId} with {DeleteOptions} strategy", id, deleteOptions);

            await _cosmosRepository.DeleteItemAsync(id, partitionKey, deleteOptions, cancellationToken);

            _logger?.LogInformation("Successfully deleted document {DocumentId} using {DeleteOptions}", id,
                deleteOptions);
        }
        catch (CosmosException ex)
        {
            _logger?.LogError(ex, "Failed to delete document {DocumentId}: {StatusCode} - {Message}",
                id, ex.StatusCode, ex.Message);
            throw new CosmoBaseException($"Failed to delete document {id}: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error deleting document {DocumentId}", id);
            throw new CosmoBaseException($"Unexpected error during document deletion: {ex.Message}", ex);
        }
    }

    Task<bool> IDataWriteService<TDto, string>.DeleteAsync(
        string id,
        CancellationToken cancellationToken)
    {
        // either throw to force the new method:
        throw new CosmoBaseException(
            "Cosmos DB deletes require both id & partition key. " +
            "Use DeleteDocumentAsync(id, partitionKey, deleteOptions) instead.");
    }

    #endregion
}