using CosmoBase.Abstractions.Filters;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Database-agnostic read operations for an entity of type <typeparamref name="T"/>
/// identified by <typeparamref name="TKey"/>.
/// </summary>
public interface IDataReadService<T, in TKey>
{
    /// <summary>Fetch a single entity by its key, or null if not found.</summary>
    Task<T?> GetByIdAsync(
        TKey id,
        CancellationToken cancellationToken = default);

    /// <summary>Stream all entities of type <typeparamref name="T"/>.</summary>
    IAsyncEnumerable<T> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Query entities using a specification.</summary>
    IAsyncEnumerable<T> QueryAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default);
}