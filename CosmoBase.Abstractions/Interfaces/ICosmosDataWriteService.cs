using CosmoBase.Abstractions.Enums;
using CosmoBase.Abstractions.Exceptions;
using CosmoBase.Abstractions.Filters;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Provides high-level write operations for Cosmos DB documents with automatic audit field management,
/// validation, and comprehensive error handling. This service abstracts away Cosmos DB implementation
/// details and provides a clean domain-focused API for data persistence operations.
/// </summary>
/// <typeparam name="T">The document type that implements <see cref="ICosmosDataModel"/>.</typeparam>
/// <remarks>
/// This service automatically manages audit fields for all operations:
/// - **Creation operations**: Set CreatedOnUtc, UpdatedOnUtc, CreatedBy, UpdatedBy, and Deleted = false
/// - **Update operations**: Update UpdatedOnUtc and UpdatedBy while preserving creation audit fields
/// - **Delete operations**: Handle both soft-delete (mark as deleted) and hard-delete (permanent removal)
/// 
/// All operations include comprehensive validation, automatic retry policies for transient failures,
/// and detailed telemetry for monitoring and debugging. Bulk operations are optimized for high
/// throughput with configurable parallelism and batch sizing.
/// </remarks>
public interface ICosmosDataWriteService<T> : IDataWriteService<T, string>
{
    #region Single Document Operations

    /// <summary>
    /// Creates a new document with comprehensive audit field management and validation.
    /// </summary>
    /// <param name="document">The document to create. Must have a unique identifier and valid partition key.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created document with populated audit fields and any server-generated properties.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    /// <exception cref="CosmoBaseException">
    /// Thrown when the document already exists, validation fails, or Cosmos DB returns an error.
    /// </exception>
    /// <remarks>
    /// **Automatic Audit Field Management:**
    /// - CreatedOnUtc: Current UTC timestamp
    /// - UpdatedOnUtc: Current UTC timestamp
    /// - CreatedBy: Current user from configured user context
    /// - UpdatedBy: Current user from configured user context
    /// - Deleted: false
    /// 
    /// **Validation performed:**
    /// - Document structure and required fields
    /// - Partition key consistency
    /// - Business rule validation (if configured)
    /// 
    /// **Performance characteristics:**
    /// - Single round-trip to Cosmos DB
    /// - Automatic retry for transient failures
    /// - Request unit consumption logged for cost monitoring
    /// </remarks>
    new Task<T> CreateAsync(T document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing document with intelligent audit field management.
    /// </summary>
    /// <param name="document">
    /// The document to replace. Must contain the same ID and partition key as the existing document.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The replaced document with updated audit fields.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    /// <exception cref="CosmoBaseException">
    /// Thrown when the document doesn't exist, validation fails, or Cosmos DB returns an error.
    /// </exception>
    /// <remarks>
    /// **Intelligent Audit Field Management:**
    /// - UpdatedOnUtc: Current UTC timestamp
    /// - UpdatedBy: Current user from configured user context
    /// - CreatedOnUtc, CreatedBy: Preserved from the original document
    /// - Deleted: Preserved unless explicitly modified
    /// 
    /// **Important considerations:**
    /// - This operation requires the document to already exist (will fail for new documents)
    /// - Uses optimistic concurrency - may fail if document was modified after retrieval
    /// - Completely replaces the document (not a partial update)
    /// - For partial updates, consider using PatchDocumentAsync instead
    /// 
    /// **Performance characteristics:**
    /// - Single round-trip to Cosmos DB
    /// - Higher RU cost than patch operations for small changes
    /// - Automatic retry for transient failures
    /// </remarks>
    Task<T> ReplaceAsync(T document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new document or replaces an existing one with intelligent audit field management (upsert semantics).
    /// </summary>
    /// <param name="document">The document to create or replace.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The upserted document with appropriate audit fields based on whether it was created or updated.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    /// <exception cref="CosmoBaseException">
    /// Thrown when validation fails or Cosmos DB returns an error.
    /// </exception>
    /// <remarks>
    /// **Intelligent Audit Field Management:**
    /// 
    /// *For new documents (create scenario):*
    /// - CreatedOnUtc: Current UTC timestamp
    /// - UpdatedOnUtc: Current UTC timestamp
    /// - CreatedBy: Current user from configured user context
    /// - UpdatedBy: Current user from configured user context
    /// - Deleted: false
    /// 
    /// *For existing documents (replace scenario):*
    /// - UpdatedOnUtc: Current UTC timestamp
    /// - UpdatedBy: Current user from configured user context
    /// - CreatedOnUtc, CreatedBy: Preserved from original document
    /// - Deleted: Preserved unless explicitly modified
    /// 
    /// **When to use Upsert vs Create/Replace:**
    /// - Use when you don't know if the document exists
    /// - Ideal for idempotent operations
    /// - Common in data synchronization scenarios
    /// - Reduces complexity by handling both cases automatically
    /// 
    /// **Performance characteristics:**
    /// - Single round-trip to Cosmos DB regardless of create/update
    /// - Slightly higher RU cost than dedicated Create/Replace operations
    /// - Automatic retry for transient failures
    /// - Cache invalidation only occurs for actual creates (HTTP 201)
    /// </remarks>
    new Task<T> UpsertAsync(T document, CancellationToken cancellationToken = default);

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Creates or replaces multiple documents in parallel batches with comprehensive error handling and audit management.
    /// </summary>
    /// <param name="documents">The collection of documents to upsert.</param>
    /// <param name="partitionKeySelector">
    /// Function to extract the partition key value from each document. Must return consistent values
    /// that match the document's stored partition key property.
    /// </param>
    /// <param name="configureItem">
    /// Optional action to configure each document before upserting (e.g., set additional properties,
    /// perform validation, apply business rules). Executed before audit field management.
    /// </param>
    /// <param name="batchSize">
    /// Maximum number of documents per transactional batch. Cosmos DB limit is 100 items per batch.
    /// Larger batches improve throughput but may hit size limits. Default: 100.
    /// </param>
    /// <param name="maxConcurrency">
    /// Maximum number of parallel batch operations. Higher concurrency improves throughput but may
    /// overwhelm Cosmos DB or exceed rate limits. Default: 10.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the entire bulk operation.</param>
    /// <returns>A task that completes when all documents have been processed successfully.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="documents"/> or <paramref name="partitionKeySelector"/> is null.
    /// </exception>
    /// <exception cref="CosmoBaseException">
    /// Thrown when any documents fail to upsert. Contains detailed failure information in 
    /// <c>ex.Data["BulkUpsertResult"]</c> for programmatic access to success/failure breakdown.
    /// </exception>
    /// <remarks>
    /// **Batch Processing Strategy:**
    /// - Documents are grouped by partition key for optimal performance
    /// - Each batch is processed as a transactional unit
    /// - Failed batches are analyzed for individual item failures
    /// - Partial successes are tracked for retry scenarios
    /// 
    /// **Audit Field Management:**
    /// - Each document receives appropriate audit fields based on create/update status
    /// - Bulk operations are optimized to minimize per-item overhead
    /// - User context is resolved once per batch for efficiency
    /// 
    /// **Error Handling and Resilience:**
    /// - Automatic retry policies for transient failures (rate limiting, timeouts)
    /// - Individual item failure tracking within failed batches
    /// - Detailed exception data for implementing custom retry logic
    /// - Telemetry and logging for operational monitoring
    /// 
    /// **Performance Optimization Tips:**
    /// - Use partition key grouping for better throughput
    /// - Adjust batchSize based on document size (smaller docs = larger batches)
    /// - Monitor RU consumption and adjust maxConcurrency accordingly
    /// - Consider pre-sorting documents by partition key
    /// 
    /// **Cache Management:**
    /// - Count caches are automatically invalidated for affected partitions
    /// - Invalidation occurs only after successful completion of batches
    /// - Partial failures still invalidate cache for successful items
    /// </remarks>
    Task BulkUpsertAsync(
        IEnumerable<T> documents,
        Func<T, string> partitionKeySelector,
        Action<T>? configureItem = null,
        int batchSize = 100,
        int maxConcurrency = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates multiple new documents in parallel batches with comprehensive error handling (fails if any already exist).
    /// </summary>
    /// <param name="documents">The collection of documents to insert.</param>
    /// <param name="partitionKeySelector">
    /// Function to extract the partition key value from each document. Must return consistent values
    /// that match the document's stored partition key property.
    /// </param>
    /// <param name="configureItem">
    /// Optional action to configure each document before insertion (e.g., set additional properties,
    /// generate IDs, apply business rules). Executed before audit field management.
    /// </param>
    /// <param name="batchSize">
    /// Maximum number of documents per transactional batch. Cosmos DB limit is 100 items per batch.
    /// Larger batches improve throughput but may hit size limits. Default: 100.
    /// </param>
    /// <param name="maxConcurrency">
    /// Maximum number of parallel batch operations. Higher concurrency improves throughput but may
    /// overwhelm Cosmos DB or exceed rate limits. Default: 10.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the entire bulk operation.</param>
    /// <returns>A task that completes when all documents have been inserted successfully.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="documents"/> or <paramref name="partitionKeySelector"/> is null.
    /// </exception>
    /// <exception cref="CosmoBaseException">
    /// Thrown when any documents fail to insert (including conflicts for existing documents).
    /// Contains detailed failure information in <c>ex.Data["BulkInsertResult"]</c> for programmatic 
    /// access to success/failure breakdown.
    /// </exception>
    /// <remarks>
    /// **When to use BulkInsert vs BulkUpsert:**
    /// - Use BulkInsert when you know all documents are new
    /// - Provides better error detection for duplicate documents
    /// - Slightly better performance than upsert for pure creation scenarios
    /// - Ideal for initial data loads or import operations
    /// 
    /// **Audit Field Management:**
    /// - All documents receive complete audit field population for creation
    /// - CreatedOnUtc, UpdatedOnUtc: Current UTC timestamp
    /// - CreatedBy, UpdatedBy: Current user from configured user context
    /// - Deleted: false
    /// - Bulk operations optimize audit field updates for performance
    /// 
    /// **Conflict Handling:**
    /// - Operation fails immediately if any document already exists
    /// - Provides detailed information about which documents caused conflicts
    /// - Consider using BulkUpsertAsync if some documents might already exist
    /// - Failed items can be retried individually or with different operations
    /// 
    /// **Performance Optimization:**
    /// - Same optimization strategies as BulkUpsertAsync apply
    /// - Consider pre-validating document uniqueness for large datasets
    /// - Monitor and adjust concurrency based on Cosmos DB performance
    /// - Use telemetry data to optimize batch sizes for your document types
    /// 
    /// **Cache and Consistency:**
    /// - Count caches are automatically invalidated for affected partitions
    /// - All successful inserts increment partition document counts
    /// - Cache invalidation is atomic with batch success
    /// </remarks>
    Task BulkInsertAsync(
        IEnumerable<T> documents,
        Func<T, string> partitionKeySelector,
        Action<T>? configureItem = null,
        int batchSize = 100,
        int maxConcurrency = 10,
        CancellationToken cancellationToken = default);

    #endregion

    #region Patch Operations

    /// <summary>
    /// Applies multiple patch operations atomically to a single document with comprehensive validation and error handling.
    /// </summary>
    /// <param name="id">The unique identifier of the document to patch.</param>
    /// <param name="partitionKey">The partition key value for the document.</param>
    /// <param name="patchSpec">
    /// The patch specification containing the operations to apply. Supports add, replace, remove, 
    /// increment, and other Cosmos DB patch operations.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// The patched document with all modifications applied, or null if the document was not found.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="id"/>, <paramref name="partitionKey"/>, or <paramref name="patchSpec"/> is null.
    /// </exception>
    /// <exception cref="CosmoBaseException">
    /// Thrown when patch validation fails, the patch operations are invalid, or Cosmos DB returns an error.
    /// </exception>
    /// <remarks>
    /// **Patch vs Replace Trade-offs:**
    /// - **Patch advantages**: Lower RU cost for small changes, atomic field updates, no need to retrieve full document
    /// - **Replace advantages**: Full document validation, automatic audit field management, simpler error handling
    /// - **Use patch when**: Making small, targeted changes to large documents or high-frequency updates
    /// - **Use replace when**: Making substantial changes or when audit field management is critical
    /// 
    /// **Audit Field Considerations:**
    /// - Patch operations do NOT automatically update audit fields (UpdatedOnUtc, UpdatedBy)
    /// - Include audit field updates in your patch specification if needed
    /// - Consider using ReplaceAsync if automatic audit field management is required
    /// 
    /// **Supported Patch Operations:**
    /// - Add: Insert new properties or array elements
    /// - Replace: Update existing property values
    /// - Remove: Delete properties or array elements
    /// - Increment: Atomic numeric increments/decrements
    /// - Set: Conditional property updates
    /// 
    /// **Atomicity and Consistency:**
    /// - All patch operations in the specification are applied atomically
    /// - Either all operations succeed or none are applied
    /// - Uses optimistic concurrency - may fail if document changes during operation
    /// - Automatic retry policies handle transient failures
    /// 
    /// **Performance Characteristics:**
    /// - Lower RU consumption than replace operations for targeted updates
    /// - Single round-trip to Cosmos DB
    /// - Server-side operation reduces network overhead
    /// - Ideal for high-frequency counter updates or status changes
    /// </remarks>
    Task<T?> PatchDocumentAsync(
        string id,
        string partitionKey,
        PatchSpecification patchSpec,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Provides a convenient way to patch a specific element within an array property of a document.
    /// </summary>
    /// <param name="id">The unique identifier of the document containing the array.</param>
    /// <param name="partitionKey">The partition key value for the document.</param>
    /// <param name="listPropertyName">The name of the array/list property to modify.</param>
    /// <param name="listItemId">The identifier of the specific array element to update.</param>
    /// <param name="parameterName">The name of the property within the array element to modify.</param>
    /// <param name="replacementValue">The new value to set for the specified property.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// The patched document with the array element modified, or null if the document or array element was not found.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any of the string parameters is null or empty, or when <paramref name="replacementValue"/> is null.
    /// </exception>
    /// <exception cref="CosmoBaseException">
    /// Thrown when the document is not found, the array element is not found, or Cosmos DB returns an error.
    /// </exception>
    /// <remarks>
    /// **Use Cases:**
    /// - Updating status of items in order line items
    /// - Modifying user preferences in a settings array
    /// - Changing metadata for attachments or comments
    /// - Atomic updates to complex nested structures
    /// 
    /// **Implementation Details:**
    /// - Internally translates to a JSON Path expression like: `listPropertyName[?(@.id=='listItemId')].parameterName`
    /// - Uses Cosmos DB's server-side patch functionality for atomic updates
    /// - Requires the array elements to have an identifiable property (typically 'id')
    /// 
    /// **Limitations and Considerations:**
    /// - Array element must exist and be identifiable by the listItemId
    /// - Only updates a single property per call - use PatchDocumentAsync for multiple updates
    /// - Does not automatically update audit fields - include in patch if needed
    /// - Performance scales with array size for element lookup
    /// 
    /// **Alternative Approaches:**
    /// - For multiple array element updates, consider using PatchDocumentAsync with multiple operations
    /// - For frequent array modifications, consider document structure redesign
    /// - For complex array operations, retrieve-modify-replace pattern might be more suitable
    /// 
    /// **Example Usage:**
    /// ```csharp
    /// // Update the status of order line item with ID "item-123"
    /// await service.PatchDocumentListItemAsync(
    ///     orderId, 
    ///     partitionKey, 
    ///     "lineItems", 
    ///     "item-123", 
    ///     "status", 
    ///     "shipped"
    /// );
    /// ```
    /// </remarks>
    Task<T?> PatchDocumentListItemAsync(
        string id,
        string partitionKey,
        string listPropertyName,
        string listItemId,
        string parameterName,
        object replacementValue,
        CancellationToken cancellationToken = default);

    #endregion

    #region Delete Operations

    /// <summary>
    /// Deletes a document with automatic audit field management for soft deletes and comprehensive error handling.
    /// </summary>
    /// <param name="id">The unique identifier of the document to delete.</param>
    /// <param name="partitionKey">
    /// The partition key value for the document. This must exactly match the value stored on the document.
    /// </param>
    /// <param name="deleteOptions">
    /// Specifies the deletion strategy:
    /// - <see cref="DeleteOptions.SoftDelete"/>: Marks the document as deleted while preserving it for audit trails
    /// - <see cref="DeleteOptions.HardDelete"/>: Permanently removes the document from storage
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the delete operation finishes successfully.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="id"/> or <paramref name="partitionKey"/> is null or empty.
    /// </exception>
    /// <exception cref="CosmoBaseException">
    /// Thrown when the document is not found (for hard deletes) or when Cosmos DB returns an error.
    /// </exception>
    /// <remarks>
    /// **Soft Delete vs Hard Delete:**
    /// 
    /// *Soft Delete (DeleteOptions.SoftDelete):*
    /// - Document remains in storage but is marked as deleted
    /// - Audit fields updated: Deleted = true, UpdatedOnUtc = current time, UpdatedBy = current user
    /// - Excluded from standard queries but accessible with includeDeleted = true
    /// - Preserves data for audit trails, compliance, and potential recovery
    /// - Maintains referential integrity for related documents
    /// - Higher storage costs but better data governance
    /// 
    /// *Hard Delete (DeleteOptions.HardDelete):*
    /// - Document permanently removed from storage
    /// - Cannot be recovered once operation completes
    /// - Lower storage costs and better performance for large datasets
    /// - No audit trail of the deletion (beyond external logging)
    /// - May break referential integrity if other documents reference this one
    /// 
    /// **Audit Field Management (Soft Delete Only):**
    /// - Deleted: Set to true
    /// - UpdatedOnUtc: Current UTC timestamp
    /// - UpdatedBy: Current user from configured user context
    /// - CreatedOnUtc, CreatedBy: Preserved for audit trail
    /// 
    /// **Cache and Performance Impact:**
    /// - Count caches automatically invalidated for the affected partition
    /// - Soft deletes: Count queries exclude deleted items by default
    /// - Hard deletes: Document count immediately decreases
    /// - Both operations include automatic retry policies for reliability
    /// 
    /// **Best Practices:**
    /// - Use soft delete for user-generated content and business-critical data
    /// - Use hard delete for temporary data, logs, or when storage costs are a concern
    /// - Consider implementing scheduled cleanup jobs for old soft-deleted documents
    /// - Monitor storage costs and query performance with large numbers of soft-deleted documents
    /// 
    /// **Recovery Scenarios (Soft Delete):**
    /// - Soft-deleted documents can be "undeleted" by setting Deleted = false
    /// - Use GetItemAsync with includeDeleted = true to retrieve soft-deleted documents
    /// - Consider implementing business logic for user-initiated recovery operations
    /// </remarks>
    Task DeleteDocumentAsync(
        string id,
        string partitionKey,
        DeleteOptions deleteOptions,
        CancellationToken cancellationToken = default);

    #endregion
}