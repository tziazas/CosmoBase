using CosmoBase.Abstractions.Filters;
using Microsoft.Azure.Cosmos;

namespace CosmoBase.Core.Extensions;

internal static class PatchSpecificationExtensions
{
    /// <summary>
    /// Maps each <see cref="PatchOperationSpecification"/> to a <see cref="PatchOperation"/>,
    /// returning an <see cref="IReadOnlyList{PatchOperation}"/> for use with the Cosmos SDK.
    /// </summary>
    /// <param name="spec">The abstract patch specification containing operations.</param>
    /// <returns>A list of SDK <see cref="PatchOperation"/> objects.</returns>
    public static IReadOnlyList<PatchOperation> ToCosmosPatchOperations(this PatchSpecification spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        return spec.Operations
            .Select(op => op.Type switch
            {
                Abstractions.Enums.PatchOperationType.Add     => PatchOperation.Add(op.Path, op.Value),
                Abstractions.Enums.PatchOperationType.Replace => PatchOperation.Replace(op.Path, op.Value),
                Abstractions.Enums.PatchOperationType.Remove  => PatchOperation.Remove(op.Path),
                _ => throw new NotSupportedException($"Unsupported patch type {op.Type}")
            })
            .ToArray();
    }

    public static PatchOperationType ToCosmosPatchType(this Abstractions.Enums.PatchOperationType type)
    {
        return type switch
        {
            Abstractions.Enums.PatchOperationType.Add     => PatchOperationType.Add,
            Abstractions.Enums.PatchOperationType.Replace => PatchOperationType.Replace,
            Abstractions.Enums.PatchOperationType.Remove  => PatchOperationType.Remove,
            _ => throw new NotSupportedException($"Unsupported patch type {type}")
        };
    }
}