using System.Collections.ObjectModel;

namespace CosmoBase.Abstractions.Models;

/// <summary>
/// A Cosmos‐style SQL query: text + zero or more parameters.
/// This is done so we avoid adding QueryDefinition and - as a result of that - bringing in Cosmos package
/// dependencies. This keeps the Abstractions package clean.
/// </summary>
public class SqlQuery
{
    public string QueryText { get; }
    public IReadOnlyDictionary<string, object>? Parameters { get; }

    public SqlQuery(string queryText, IDictionary<string, object>? parameters = null)
    {
        QueryText = queryText ?? throw new ArgumentNullException(nameof(queryText));
        Parameters = parameters == null
            ? null
            : new ReadOnlyDictionary<string, object>(parameters);
    }
}