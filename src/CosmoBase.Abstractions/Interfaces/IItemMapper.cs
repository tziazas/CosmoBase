using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Defines bi‐directional mapping between the Cosmos storage model (DAO) and
/// application data transfer objects (DTO).
/// </summary>
/// <typeparam name="TDao">
/// The data access object type stored in Cosmos DB. Must have a parameterless constructor.
/// </typeparam>
/// <typeparam name="TDto">
/// The data transfer object type used by application code. Must have a parameterless constructor.
/// </typeparam>
public interface IItemMapper<TDao, TDto>
    where TDao : class, new()
    where TDto : class, new()
{
    /// <summary>
    /// Converts a DTO into its corresponding DAO for storage.
    /// </summary>
    /// <param name="dto">The DTO instance to map.</param>
    /// <returns>
    /// A new DAO instance containing the data from <paramref name="dto"/>.
    /// </returns>
    TDao ToDao(TDto dto);

    /// <summary>
    /// Converts a DAO back into its corresponding DTO for consumption.
    /// </summary>
    /// <param name="dao">The DAO instance to map.</param>
    /// <returns>
    /// A new DTO instance containing the data from <paramref name="dao"/>.
    /// </returns>
    TDto FromDao(TDao dao);

    /// <summary>
    /// Maps a sequence of DAOs to a sequence of DTOs in one operation.
    /// </summary>
    /// <param name="daos">The collection of DAOs to map.</param>
    /// <returns>
    /// An <see cref="IEnumerable{TDto}"/> of DTOs resulting from mapping each DAO.
    /// </returns>
    IEnumerable<TDto> FromDaos(IEnumerable<TDao> daos)
        => daos.Select(FromDao);
    
    /// <summary>
    /// Asynchronously maps a stream of DAOs into DTOs.
    /// </summary>
    /// <param name="daos">An async‐stream of DAOs.</param>
    /// <returns>An async‐stream of DTOs produced by mapping each DAO.</returns>
    IAsyncEnumerable<TDto> FromDaosAsync(IAsyncEnumerable<TDao> daos)
    {
        if (daos is null) throw new ArgumentNullException(nameof(daos));
        return Core();

        async IAsyncEnumerable<TDto> Core()
        {
            await foreach (var dao in daos.ConfigureAwait(false))
            {
                yield return FromDao(dao);
            }
        }
    }
}