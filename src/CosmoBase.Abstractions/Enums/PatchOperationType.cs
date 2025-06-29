namespace CosmoBase.Abstractions.Enums;

/// <summary>
/// The type of patch operation to perform on a document or document property.
/// </summary>
public enum PatchOperationType
{
    Add,
    Replace,
    Remove,
    Set,
    Increment,
}