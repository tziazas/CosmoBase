using System.Runtime.CompilerServices;
using CosmoBase.Abstractions.Filters;
using CosmoBase.Abstractions.Interfaces;

namespace CosmoBase.DataServices;

public class CosmosDataReadService<TDto, TDao>(
    ICosmosRepository<TDao> cosmosRepository,
    IItemMapper<TDao, TDto> mapper
) : IDataReadService<TDto>
    where TDao : class, new()
    where TDto : class, new()
{
    public async IAsyncEnumerable<TDto> GetAllAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await foreach (var dao in cosmosRepository.GetAll(cancellationToken))
        {
            yield return mapper.FromDao(dao);
        }
    }

    public async IAsyncEnumerable<TDto> GetAllAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken cancellationToken,
        int limit,
        int offset,
        int count
    )
    {
        await foreach (var dao in cosmosRepository.GetAll(cancellationToken))
        {
            yield return mapper.FromDao(dao);
        }
    }

    public async IAsyncEnumerable<TDto> GetAllAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken cancellationToken,
        string partitionKey
    )
    {
        await foreach (var dao in cosmosRepository.GetAll(cancellationToken, partitionKey))
        {
            yield return mapper.FromDao(dao);
        }
    }

    public async Task<IList<TDto>> GetAllByArrayPropertyAsync(
        CancellationToken cancellationToken,
        string arrayName,
        string arrayPropertyName,
        string propertyValue
    )
    {
        return mapper.FromDaos(
            await cosmosRepository.GetAllByArrayPropertyAsync(
                arrayName,
                arrayPropertyName,
                propertyValue
            )
        ).ToList();
    }

    public async Task<IList<TDto>> GetAllByPropertyAsync(
        CancellationToken cancellationToken,
        string propertyName,
        string propertyValue
    )
    {
        return mapper.FromDaos(
            await cosmosRepository.GetAllByPropertyAsync(propertyName, propertyValue)
        ).ToList();
    }

    public async Task<IList<TDto>> GetAllByPropertyComparisonAsync(
        CancellationToken cancellationToken,
        List<PropertyFilter> propertyFilters
    )
    {
        return mapper.FromDaos(
            await cosmosRepository.GetAllByPropertyComparisonAsync(propertyFilters)
        ).ToList();
    }

    public async Task<IList<TDto>> GetAllDistinctInListByPropertyAsync(
        CancellationToken cancellationToken,
        string propertyName,
        List<string> propertyValueList
    )
    {
        var data = await cosmosRepository.GetAllDistinctInListByPropertyAsync(
            cancellationToken,
            propertyName,
            propertyValueList
        );

        return mapper.FromDaos(data).ToList();
    }

    public async Task<TDto?> GetByIdAsync(string id)
    {
        var dao = await cosmosRepository.GetByIdAsync(id);

        // Here we don't cast, we actually want to use IMapper
        return dao == null ? null : mapper.FromDao(dao);
    }

    public async Task<TDto?> GetByIdAsync(string id, string partitionKey)
    {
        var dao = await cosmosRepository.GetByIdAsync(id, partitionKey);
        
        return dao == null ? null : mapper.FromDao(dao);
    }

    public async IAsyncEnumerable<TDto> GetByQuery(
        [EnumeratorCancellation] CancellationToken cancellationToken,
        string query
    )
    {
        var items = cosmosRepository.GetByQuery(cancellationToken, query);
        await foreach (var dao in items)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            yield return mapper.FromDao(dao);
        }
    }
}