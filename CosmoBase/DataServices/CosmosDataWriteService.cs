using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Interfaces;

namespace CosmoBase.DataServices;

public class CosmosDataWriteService<TDto, TDao>(
    ICosmosRepository<TDao> cosmosRepository,
    IItemMapper<TDao, TDto> mapper
) : IDataWriteService<TDto>
    where TDto : class, new()
    where TDao : class, new()
{
    public async Task DeleteAsync(string id)
    {
        await cosmosRepository.DeleteAsync(id, DeleteOptions.SoftDelete);
    }

    public async Task DeleteAsync(string id, string partitionKey)
    {
        await cosmosRepository.DeleteAsync(id, partitionKey, DeleteOptions.SoftDelete);
    }

    public async Task<TDto> SaveAsync(TDto document)
    {
        var dao = (ICosmosDataModel)mapper.ToDao(document);
        var exists = await cosmosRepository.GetByIdAsync(dao.Id);

        if (exists is null)
        {
            var resultingDao = await cosmosRepository.CreateAsync(dao);
            return mapper.FromDao(resultingDao);
        }
        else
        {
            // If the item exists, keep in mind that changing the partition key
            // (in this case StoreId) will create a duplicate record.
            var response = await cosmosRepository.UpdateAsync(dao);
            return mapper.FromDao(response);
        }
    }
}