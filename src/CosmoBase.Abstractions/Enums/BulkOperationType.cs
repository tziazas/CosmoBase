namespace CosmoBase.Abstractions.Enums;

/// <summary>
/// Specifies the type of bulk operation being performed.
/// </summary>
public enum BulkOperationType
{
    /// <summary>
    /// Bulk create operation - all items are new documents.
    /// </summary>
    Create,
    
    /// <summary>
    /// Bulk upsert operation - items may be of type create or update.
    /// </summary>
    Upsert
}