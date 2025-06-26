using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Exceptions;
using CosmoBase.Abstractions.Filters;
using CosmoBase.Abstractions.Interfaces;
using Microsoft.Azure.Cosmos;
using PatchOperationType = CosmoBase.Abstractions.Enums.PatchOperationType;

namespace CosmoBase.DataServices;

/// <summary>
/// Concrete implementation of <see cref="ICosmosDataWriteService{T}"/> using an underlying <see cref="ICosmosRepository{TDao}"/> and <see cref="IItemMapper{TDao,TDto}"/>.
/// </summary>
/// <typeparam name="TDto">The DTO type used by application code.</typeparam>
/// <typeparam name="TDao">The DAO type stored in Cosmos DB.</typeparam>
public class CosmosDataWriteService<TDto, TDao> : ICosmosDataWriteService<TDto>
    where TDto : class, new()
    where TDao : class, ICosmosDataModel, new()
{
    private readonly ICosmosRepository<TDao> _cosmosRepository;
    private readonly IItemMapper<TDao, TDto> _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDataWriteService{TDto,TDao}"/> class.
    /// </summary>
    public CosmosDataWriteService(
        ICosmosRepository<TDao> cosmosRepository,
        IItemMapper<TDao, TDto> mapper)
    {
        _cosmosRepository = cosmosRepository ?? throw new ArgumentNullException(nameof(cosmosRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <inheritdoc />
    public async Task<TDto?> CreateAsync(
        TDto entity,
        CancellationToken cancellationToken = default)
    {
        var dao = _mapper.ToDao(entity);
        var created = await _cosmosRepository.CreateItemAsync(dao, cancellationToken);
        return _mapper.FromDao(created);
    }

    /// <inheritdoc />
    public async Task<TDto> UpsertAsync(
        TDto entity,
        CancellationToken cancellationToken = default)
    {
        var dao = _mapper.ToDao(entity);
        var upsertItemAsync = await _cosmosRepository.UpsertItemAsync(dao, cancellationToken);
        return _mapper.FromDao(upsertItemAsync);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        throw new CosmoBaseException($"Cosmos does not support delete operations without a partition key : {id}");
    }

    /// <inheritdoc />
    public async Task BulkUpsertAsync(
        IEnumerable<TDto> items,
        Func<TDto, string> partitionKeySelector,
        Action<TDto>? configureItem = null,
        int batchSize = 100,
        int maxConcurrency = 10,
        CancellationToken cancellationToken = default)
    {
        // materialize the incoming enumerable only once
        var dtoList = items.ToList();
        
        var daoItems = dtoList
            .Select(dto =>
            {
                configureItem?.Invoke(dto);
                return _mapper.ToDao(dto);
            })
            .ToList();
        
        var pk = dtoList
            .Select(partitionKeySelector)
            .Distinct()
            .Single();

        await _cosmosRepository
            .BulkUpsertAsync(
                daoItems,
                pk,
                batchSize,
                maxConcurrency,
                cancellationToken);
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
        // materialize the incoming enumerable only once
        var dtoList = items.ToList();
        
        var daoItems = dtoList
            .Select(dto =>
            {
                configureItem?.Invoke(dto);
                return _mapper.ToDao(dto);
            })
            .ToList();
        
        var pk = dtoList
            .Select(partitionKeySelector)
            .Distinct()
            .Single();

        await _cosmosRepository
            .BulkInsertAsync(
                daoItems,
                pk,
                batchSize,
                maxConcurrency,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TDto?> PatchDocumentAsync(
        string id,
        string partitionKey,
        PatchSpecification patchSpec,
        CancellationToken cancellationToken = default)
    {
        var patchedDao = await _cosmosRepository
            .PatchItemAsync(id, partitionKey, patchSpec, cancellationToken);

        return patchedDao == null ? null : _mapper.FromDao(patchedDao);
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
        var path = $"{listPropertyName}[?(@.id=='{listItemId}')].{parameterName}";
        var patch = new PatchSpecification(
            [new PatchOperationSpecification(path, PatchOperationType.Replace, replacementValue)]);

        return await PatchDocumentAsync(
            id,
            partitionKey,
            patch,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteDocumentAsync(
        string id,
        string partitionKey,
        DeleteOptions deleteOptions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _cosmosRepository.DeleteItemAsync(id, partitionKey, deleteOptions, cancellationToken);
        }
        catch (CosmosException e)
        {
            throw new CosmoBaseException(e.Message, e);
        }
    }
}