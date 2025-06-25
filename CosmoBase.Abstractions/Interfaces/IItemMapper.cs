namespace CosmoBase.Abstractions.Interfaces;

public interface IItemMapper<TDao, TDto>
    where TDao : class, new()
    where TDto : class, new()
{
    TDao ToDao(TDto dto);
    TDto FromDao(TDao dao);
    
    /// <summary>Map an entire collection in one call.</summary>
    IEnumerable<TDto> FromDaos(IEnumerable<TDao> daos)
        => daos.Select(FromDao);
}