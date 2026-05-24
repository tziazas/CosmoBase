using System;
using System.Collections.Generic;
using System.Linq;
using CosmoBase.Abstractions.Filters;
using Microsoft.Azure.Cosmos;

namespace CosmoBase.Core.Extensions;

/// <summary>
/// Helpers to build SQL and parameters from <see cref="PropertyFilter"/> collections.
/// </summary>
public static class PropertyFilterExtensions
{
    /// <summary>
    /// Builds the SQL WHERE clause (without the leading "WHERE ") for the given filters.
    /// All values — including those in IN clauses — are emitted as named parameters;
    /// call <see cref="AddParameters"/> with the same list to bind them.
    /// </summary>
    public static string BuildSqlWhereClause(this IEnumerable<PropertyFilter> filters)
    {
        if (filters == null) throw new ArgumentNullException(nameof(filters));

        var filterList = filters as IList<PropertyFilter> ?? filters.ToList();
        var parts = new List<string>();

        for (var idx = 0; idx < filterList.Count; idx++)
        {
            var f = filterList[idx];
            var col = f.PropertyName.StartsWith("@") ? f.PropertyName.Substring(1) : f.PropertyName;

            switch (f.PropertyComparison)
            {
                case PropertyComparison.Equal:
                    parts.Add($"c.{col} = {f.PropertyName}");
                    break;
                case PropertyComparison.NotEqual:
                    parts.Add($"c.{col} <> {f.PropertyName}");
                    break;
                case PropertyComparison.GreaterThan:
                    parts.Add($"c.{col} > {f.PropertyName}");
                    break;
                case PropertyComparison.LessThan:
                    parts.Add($"c.{col} < {f.PropertyName}");
                    break;
                case PropertyComparison.GreaterThanOrEqual:
                    parts.Add($"c.{col} >= {f.PropertyName}");
                    break;
                case PropertyComparison.LessThanOrEqual:
                    parts.Add($"c.{col} <= {f.PropertyName}");
                    break;
                case PropertyComparison.In:
                    var values = ((IEnumerable<object>)f.PropertyValue).ToList();
                    var paramNames = values.Select((_, i) => $"@{col}_{idx}_in_{i}");
                    parts.Add($"c.{col} IN ({string.Join(", ", paramNames)})");
                    break;
                default:
                    throw new NotSupportedException($"Comparison {f.PropertyComparison} not supported");
            }
        }

        return parts.Count == 0
            ? "1=1"
            : string.Join(" AND ", parts);
    }

    /// <summary>
    /// Binds all filter values into <paramref name="def"/> as named parameters,
    /// including each individual value from IN-list filters.
    /// Must be called with the same filter list used to build the WHERE clause.
    /// </summary>
    public static void AddParameters(this IEnumerable<PropertyFilter> filters, QueryDefinition def)
    {
        if (filters == null) throw new ArgumentNullException(nameof(filters));
        if (def == null) throw new ArgumentNullException(nameof(def));

        var filterList = filters as IList<PropertyFilter> ?? filters.ToList();

        for (var idx = 0; idx < filterList.Count; idx++)
        {
            var f = filterList[idx];

            if (f.PropertyComparison == PropertyComparison.In)
            {
                var col = f.PropertyName.StartsWith("@") ? f.PropertyName.Substring(1) : f.PropertyName;
                var values = ((IEnumerable<object>)f.PropertyValue).ToList();
                for (var i = 0; i < values.Count; i++)
                    def.WithParameter($"@{col}_{idx}_in_{i}", values[i]);
                continue;
            }

            def.WithParameter(f.PropertyName, f.PropertyValue);
        }
    }
}
