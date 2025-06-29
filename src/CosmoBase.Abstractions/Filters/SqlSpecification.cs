using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CosmoBase.Abstractions.Filters;

/// <summary>
/// A SQL-style query: text + optional parameters.
/// </summary>
public class SqlSpecification<T>(
    string queryText,
    IDictionary<string, object>? parameters = null)
    : ISpecification<T>
{
    public string QueryText { get; } = queryText 
                                       ?? throw new ArgumentNullException(nameof(queryText));

    public IReadOnlyDictionary<string, object>? Parameters { get; } = parameters is null
        ? null
        : new ReadOnlyDictionary<string, object>(parameters);
}