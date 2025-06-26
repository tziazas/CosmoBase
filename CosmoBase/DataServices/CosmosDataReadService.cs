using System.Runtime.CompilerServices;
using CosmoBase.Abstractions.Exceptions;
using CosmoBase.Abstractions.Filters;
using CosmoBase.Abstractions.Interfaces;

namespace CosmoBase.DataServices;

/// <summary>
/// Concrete implementation of <see cref="ICosmosDataReadService{T}"/> using an underlying <see cref="ICosmosRepository{TDao}"/> and <see cref="IItemMapper{TDao,TDto}"/>.
/// </summary>
/// <typeparam name="TDto">The DTO type returned to callers.</typeparam>
/// <typeparam name="TDao">The DAO type stored in Cosmos DB.</typeparam>
public class CosmosDataReadService<TDto, TDao> : ICosmosDataReadService<TDto>
    where TDao : class, ICosmosDataModel, new()
    where TDto : class, new()
{
    private readonly ICosmosRepository<TDao> _cosmosRepository;
    private readonly IItemMapper<TDao, TDto> _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDataReadService{TDto,TDao}"/> class.
    /// </summary>
    /// <param name="cosmosRepository">The underlying repository for DAO operations.</param>
    /// <param name="mapper">The mapper to translate between DAO and DTO.</param>
    public CosmosDataReadService(
        ICosmosRepository<TDao> cosmosRepository,
        IItemMapper<TDao, TDto> mapper)
    {
        _cosmosRepository = cosmosRepository ?? throw new ArgumentNullException(nameof(cosmosRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TDto> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _mapper.FromDaosAsync(_cosmosRepository
            .GetAllAsync(cancellationToken));
    }

    /// <inheritdoc />
    public Task<TDto?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        throw new CosmoBaseException($"Cosmos does not support delete operations without a partition key : {id}");
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TDto> QueryAsync(
        ISpecification<TDto> specification,
        CancellationToken cancellationToken = default)
    {
        if (specification is not SqlSpecification<TDto> sqlSpecification)
            throw new ArgumentException("Specification is not a SqlSpecification", nameof(specification));

        var daoSpecification =
            new SqlSpecification<TDao>(sqlSpecification.QueryText, sqlSpecification.Parameters?.ToDictionary());
        return _mapper.FromDaosAsync(_cosmosRepository
            .QueryAsync(daoSpecification, cancellationToken));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<List<TDto>> BulkReadAsyncEnumerable(
        ISpecification<TDto> specification,
        string partitionKey,
        int batchSize = 100,
        int maxConcurrency = 50,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (specification is not SqlSpecification<TDto> sqlSpecification)
            throw new ArgumentException("Specification is not a SqlSpecification", nameof(specification));

        var daoSpecification = new SqlSpecification<TDao>(sqlSpecification.QueryText,
            sqlSpecification.Parameters?.ToDictionary());

        await foreach (var daoBatch in _cosmosRepository
                           .BulkReadAsyncEnumerable(daoSpecification, partitionKey, batchSize, maxConcurrency,
                               cancellationToken))
        {
            yield return _mapper.FromDaos(daoBatch).ToList();
        }
    }

    /// <inheritdoc />
    public Task<int> GetCountAsync(
        string partitionKeyValue,
        CancellationToken cancellationToken = default)
        => _cosmosRepository.GetCountAsync(partitionKeyValue, cancellationToken);

    /// <inheritdoc />
    public async Task<(IList<TDto> Items, string? ContinuationToken)> GetPageWithTokenAsync(
        ISpecification<TDto> specification,
        string partitionKey,
        int pageSize,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        if (specification is not SqlSpecification<TDto> sqlSpecification)
            throw new ArgumentException("Specification is not a SqlSpecification", nameof(specification));

        var daoSpecification = new SqlSpecification<TDao>(sqlSpecification.QueryText,
            sqlSpecification.Parameters?.ToDictionary());
        
        var (daoItems, token) = await _cosmosRepository
            .GetPageWithTokenAsync(daoSpecification, partitionKey, pageSize, continuationToken, cancellationToken);

        var dtoItems = _mapper.FromDaos(daoItems);

        return (dtoItems.ToList(), token);
    }

    /// <inheritdoc />
    public async Task<(IList<TDto> Items, string? ContinuationToken, int? TotalCount)> GetPageWithTokenAndCountAsync(
        ISpecification<TDto> specification,
        string partitionKey,
        int pageSize,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        if (specification is not SqlSpecification<TDto> sqlSpecification)
            throw new ArgumentException("Specification is not a SqlSpecification", nameof(specification));

        var daoSpecification = new SqlSpecification<TDao>(sqlSpecification.QueryText,
            sqlSpecification.Parameters?.ToDictionary());
        
        var (daoItems, token, totalCount) = await _cosmosRepository
            .GetPageWithTokenAndCountAsync(daoSpecification, partitionKey, pageSize, continuationToken, cancellationToken);

        var dtoItems = _mapper.FromDaos(daoItems);

        return (dtoItems.ToList(), token, totalCount);
    }
}