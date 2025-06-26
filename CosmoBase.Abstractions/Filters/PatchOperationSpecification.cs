using CosmoBase.Abstractions.Enums;

namespace CosmoBase.Abstractions.Filters;

/// <summary>
/// A single patch operation: which <see cref="Path"/>, which <see cref="Type"/>, and what <see cref="Value"/> to apply.
/// </summary>
public class PatchOperationSpecification(
    string path,
    PatchOperationType type,
    object? value = null)
{
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
    public PatchOperationType Type { get; } = type;
    public object? Value { get; } = value;
}