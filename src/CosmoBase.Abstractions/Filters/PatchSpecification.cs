using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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