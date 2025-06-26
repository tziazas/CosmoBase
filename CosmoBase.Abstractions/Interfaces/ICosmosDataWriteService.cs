using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Exceptions;
using CosmoBase.Abstractions.Filters;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Cosmos‐DB‐specific write operations on <typeparamref name="T"/>.
/// </summary>
public interface ICosmosDataWriteService<T> : IDataWriteService<T, string>
{
    /// <summary>Upsert many items in parallel.</summary>
    Task BulkUpsertAsync(
        IEnumerable<T> items,
        Func<T, string> partitionKeySelector,
        Action<T>? configureItem = null,
        int batchSize = 100,
        int maxConcurrency = 10,
        CancellationToken cancellationToken = default);

    /// <summary>Insert many items in parallel (fails if exists).</summary>
    Task BulkInsertAsync(
        IEnumerable<T> items,
        Func<T, string> partitionKeySelector,
        Action<T>? configureItem = null,
        int batchSize = 100,
        int maxConcurrency = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply multiple patch operations atomically to a single document.
    /// </summary>
    /// <param name="id">The document's id.</param>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="patchSpec">The patch operations to apply.</param>
    /// <param name="cancellationToken"></param>
    Task<T?> PatchDocumentAsync(
        string id,
        string partitionKey,
        PatchSpecification patchSpec,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience for patching a single element inside a list property.
    /// </summary>
    /// <remarks>
    /// Internally you can translate this to a path like 
    /// <c>$"{listPropertyName}[?(@.id=='{listItemId}')].{parameterName}"</c>
    /// or multiple operations in your implementation.
    /// </remarks>
    Task<T?> PatchDocumentListItemAsync(
        string id,
        string partitionKey,
        string listPropertyName,
        string listItemId,
        string parameterName,
        object replacementValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document from the Cosmos container by its identifier and partition key.
    /// </summary>
    /// <param name="id">
    /// The unique identifier of the document to delete.
    /// </param>
    /// <param name="partitionKey">
    /// The value of the partition key for the document. This must match the value stored on the document.
    /// </param>
    /// <param name="deleteOptions">
    /// Specifies whether to perform a hard delete (remove the item entirely) or a soft delete
    /// (mark the item as deleted without removing it from storage).
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while waiting for the task to complete.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous delete operation.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="id"/> or <paramref name="partitionKey"/> is <c>null</c> or empty.
    /// </exception>
    /// <exception cref="CosmoBaseException">
    /// Thrown if the Cosmos DB service returns an error during the delete operation.
    /// </exception>
    Task DeleteDocumentAsync(
        string id,
        string partitionKey,
        DeleteOptions deleteOptions,
        CancellationToken cancellationToken = default);
}