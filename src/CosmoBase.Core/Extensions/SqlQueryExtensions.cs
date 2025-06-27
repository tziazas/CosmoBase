using System;
using CosmoBase.Abstractions.Filters;
using CosmoBase.Abstractions.Models;
using Microsoft.Azure.Cosmos;

namespace CosmoBase.Core.Extensions;

/// <summary>
/// Provides extension methods to convert a <see cref="SqlQuery"/> into Cosmos DB SDK types.
/// </summary>
internal static class SqlQueryExtensions
{
    /// <summary>
    /// Converts the specified <see cref="SqlQuery"/> into a <see cref="QueryDefinition"/> suitable
    /// for use with the Azure Cosmos DB SDK, applying both the SQL text and any parameters.
    /// </summary>
    /// <returns>
    /// A <see cref="QueryDefinition"/> initialized with the SQL text and parameter values from
    /// <paramref />.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown if unsupported.
    /// </exception>
    public static QueryDefinition ToCosmosQuery<T>(this ISpecification<T> spec)
    {
        if (spec is SqlSpecification<T> sql)
        {
            var q = new QueryDefinition(sql.QueryText);
            if (sql.Parameters != null)
                foreach (var kv in sql.Parameters)
                    q.WithParameter(kv.Key, kv.Value);
            return q;
        }

        throw new NotSupportedException(
            $"Spec of type {spec.GetType().Name} is not supported.");
    }
    
    public static QueryDefinition ConvertToCountQuery<T>(this ISpecification<T> spec)
    {
        if (spec is not SqlSpecification<T> sql)
            throw new NotSupportedException(
                $"Spec of type {spec.GetType().Name} is not supported.");
        
        var originalQueryText = sql.QueryText;

        var countQueryText = System.Text.RegularExpressions.Regex.Replace(
            originalQueryText,
            @"^\s*SELECT\s+\*\s+FROM",
            "SELECT VALUE COUNT(1) FROM",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Create new QueryDefinition with same parameters
        var countQuery = new QueryDefinition(countQueryText);

        // Copy all parameters from original query
        if (sql.Parameters == null) return countQuery;
        
        foreach (var parameter in sql.Parameters)
        {
            countQuery = countQuery.WithParameter(parameter.Key, parameter.Value);
        }

        return countQuery;

    }
}