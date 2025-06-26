using System.Collections.ObjectModel;

namespace CosmoBase.Abstractions.Filters;

/// <summary>
/// A container for one or more <see cref="PatchOperationSpecification"/>s.
/// </summary>
public class PatchSpecification(IEnumerable<PatchOperationSpecification> operations)
{
    public IReadOnlyCollection<PatchOperationSpecification> Operations { get; } =
        new ReadOnlyCollection<PatchOperationSpecification>(
            operations?.ToList()
            ?? throw new ArgumentNullException(nameof(operations)));
}