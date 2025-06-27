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
    /// Builds the SQL WHERE clause (without the leading “WHERE ”) for the given filters.
    /// </summary>
    public static string BuildSqlWhereClause(this IEnumerable<PropertyFilter> filters)
    {
        if (filters == null) throw new ArgumentNullException(nameof(filters));

        var parts = new List<string>();
        foreach (var f in filters)
        {
            var col = f.PropertyName.StartsWith("@") ? f.PropertyName.Substring(1) : f.PropertyName;
            switch (f.PropertyComparison)
            {
                case PropertyComparison.Equals:
                    parts.Add($"c.{col} = {f.PropertyName}");
                    break;
                case PropertyComparison.NotEquals:
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
                    // We'll embed literals directly for IN
                    var list = (IEnumerable<object>)f.PropertyValue;
                    var inList = string.Join(", ", list.Select(v => $"'{v}'"));
                    parts.Add($"c.{col} IN ({inList})");
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
    /// Adds all non-IN parameters from your filters into the <paramref name="def"/>.
    /// </summary>
    public static void AddParameters(this IEnumerable<PropertyFilter> filters, QueryDefinition def)
    {
        if (filters == null) throw new ArgumentNullException(nameof(filters));
        if (def == null) throw new ArgumentNullException(nameof(def));

        foreach (var f in filters)
        {
            if (f.PropertyComparison == PropertyComparison.In)
                continue;   // we inlined literals for IN
            def.WithParameter(f.PropertyName, f.PropertyValue);
        }
    }
}