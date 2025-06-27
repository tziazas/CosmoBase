using CosmoBase.Abstractions.Interfaces;

namespace CosmoBase.Core.Services;

/// <summary>
/// Default implementation of audit field management.
/// </summary>
/// <typeparam name="T">The document type.</typeparam>
public class AuditFieldManager<T> : IAuditFieldManager<T> where T : class, ICosmosDataModel
{
    private readonly IUserContext _userContext;

    public AuditFieldManager(IUserContext userContext)
    {
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    }

    /// <inheritdoc/>
    public void SetCreateAuditFields(T item)
    {
        var now = DateTime.UtcNow;
        var currentUser = _userContext.GetCurrentUser();

        item.CreatedOnUtc = now;
        item.UpdatedOnUtc = now;
        item.CreatedBy = currentUser;
        item.UpdatedBy = currentUser;
        item.Deleted = false;
    }

    /// <inheritdoc/>
    public void SetUpdateAuditFields(T item)
    {
        var now = DateTime.UtcNow;
        var currentUser = _userContext.GetCurrentUser();

        item.UpdatedOnUtc = now;
        item.UpdatedBy = currentUser;

        // Ensure CreatedOnUtc is set if it wasn't already
        if (!item.CreatedOnUtc.HasValue)
        {
            item.CreatedOnUtc = now;
            item.CreatedBy = currentUser;
        }
    }

    /// <inheritdoc/>
    public void SetUpsertAuditFields(T item)
    {
        var now = DateTime.UtcNow;
        var currentUser = _userContext.GetCurrentUser();

        // Determine if this is a create or update based on existing audit fields
        var isCreate = !item.CreatedOnUtc.HasValue;

        if (isCreate)
        {
            // This is a new document
            item.CreatedOnUtc = now;
            item.CreatedBy = currentUser;
            item.Deleted = false;
        }

        // Always update the modified fields
        item.UpdatedOnUtc = now;
        item.UpdatedBy = currentUser;
    }

    /// <inheritdoc/>
    public void SetBulkAuditFields(IEnumerable<T> items, bool isCreateOperation)
    {
        var now = DateTime.UtcNow;
        var currentUser = _userContext.GetCurrentUser();

        foreach (var item in items)
        {
            if (isCreateOperation)
            {
                // Bulk create - set all audit fields
                item.CreatedOnUtc = now;
                item.UpdatedOnUtc = now;
                item.CreatedBy = currentUser;
                item.UpdatedBy = currentUser;
                item.Deleted = false;
            }
            else
            {
                // Bulk upsert - use upsert logic
                SetUpsertAuditFields(item);
            }
        }
    }
}