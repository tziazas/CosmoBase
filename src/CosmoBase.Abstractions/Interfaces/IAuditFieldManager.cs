using System.Collections.Generic;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Manages audit field population for Cosmos documents.
/// </summary>
/// <typeparam name="T">The document type.</typeparam>
public interface IAuditFieldManager<T> where T : class, ICosmosDataModel
{
    /// <summary>
    /// Sets audit fields for a document being created.
    /// </summary>
    /// <param name="item">The document to update.</param>
    void SetCreateAuditFields(T item);

    /// <summary>
    /// Sets audit fields for a document being updated.
    /// </summary>
    /// <param name="item">The document to update.</param>
    void SetUpdateAuditFields(T item);

    /// <summary>
    /// Sets audit fields for a document being upserted.
    /// Determines whether it's a create or update based on existing audit fields.
    /// </summary>
    /// <param name="item">The document to update.</param>
    void SetUpsertAuditFields(T item);

    /// <summary>
    /// Sets audit fields for documents in bulk operations.
    /// </summary>
    /// <param name="items">The documents to update.</param>
    /// <param name="isCreateOperation">Whether this is a create operation (vs update/upsert).</param>
    void SetBulkAuditFields(IEnumerable<T> items, bool isCreateOperation);
}