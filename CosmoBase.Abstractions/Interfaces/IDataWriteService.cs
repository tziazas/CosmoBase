namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Provides write‐only data operations for <typeparamref name="T"/> DTOs,
/// encapsulating create, update, and delete semantics.
/// </summary>
/// <typeparam name="T">
/// The DTO type being persisted. Must be a reference type.
/// </typeparam>
public interface IDataWriteService<T>
    where T : class
{
    /// <summary>
    /// Saves the specified object to the database.  
    /// If an existing record with the same identifier exists, it is updated;  
    /// otherwise a new record is created.
    /// </summary>
    /// <param name="obj">The object to create or update.</param>
    /// <returns>
    /// A task that completes with the saved instance of <typeparamref name="T"/>,
    /// potentially including any server‐assigned metadata (e.g., timestamps, etags).
    /// </returns>
    Task<T> SaveAsync(T obj);

    /// <summary>
    /// Deletes a document by its unique identifier, using the default
    /// partition‐key resolution strategy configured for the model.
    /// </summary>
    /// <param name="id">The unique identifier of the document to delete.</param>
    /// <returns>A task that completes when the delete operation has finished.</returns>
    Task DeleteAsync(string id);

    /// <summary>
    /// Deletes a document by its unique identifier within a specific partition.
    /// </summary>
    /// <param name="id">The unique identifier of the document to delete.</param>
    /// <param name="partitionKey">
    /// The partition‐key value where the document resides.
    /// </param>
    /// <returns>A task that completes when the delete operation has finished.</returns>
    Task DeleteAsync(string id, string partitionKey);
}