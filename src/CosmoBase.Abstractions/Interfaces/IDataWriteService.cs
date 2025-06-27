namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Database-agnostic write operations for an entity of type <typeparamref name="T"/>
/// identified by <typeparamref name="TKey"/>.
/// </summary>
public interface IDataWriteService<T, in TKey>
{
    /// <summary>Create a new entity; returns the created instance.</summary>
    Task<T?> CreateAsync(
        T entity,
        CancellationToken cancellationToken = default);

    /// <summary>Replace or insert (upsert) an entity.</summary>
    Task<T> UpsertAsync(
        T entity,
        CancellationToken cancellationToken = default);

    /// <summary>Delete an entity by key; returns true if it existed.</summary>
    Task<bool> DeleteAsync(
        TKey id,
        CancellationToken cancellationToken = default);
}