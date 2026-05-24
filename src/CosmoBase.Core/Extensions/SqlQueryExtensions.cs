using System;
using System.Text.RegularExpressions;
using CosmoBase.Abstractions.Filters;
using CosmoBase.Abstractions.Models;
using Microsoft.Azure.Cosmos;

namespace CosmoBase.Core.Extensions;

/// <summary>
/// Provides extension methods to convert a <see cref="SqlQuery"/> into Cosmos DB SDK types.
/// </summary>
internal static class SqlQueryExtensions
{
    // Pre-compiled regexes used by ConvertToCountQuery.
    //
    // Why three separate passes instead of one complex pattern?
    // Each pass has a single, clear responsibility.  A single pattern that tried
    // to match SELECT-clause + ORDER-BY + OFFSET/LIMIT simultaneously would be
    // hard to read and fragile to maintain.
    //
    // Pass 1 — SELECT clause replacement
    //   Matches everything from SELECT up to (and including) the first FROM keyword.
    //   The non-greedy .+? with RegexOptions.Singleline means '.' crosses newlines,
    //   so multi-line queries work correctly.  Non-greedy ensures we stop at the
    //   *first* FROM, not a later one that might appear in a JOIN alias.
    //   Handles: SELECT *, SELECT c.field, SELECT c.f1, c.f2, SELECT VALUE expr.
    //
    // Pass 2 — ORDER BY removal
    //   ORDER BY is syntactically invalid inside a COUNT query in Cosmos SQL and
    //   adds unnecessary RU cost.  The .+ with Singleline consumes to end-of-string
    //   so multi-line ORDER BY expressions are handled.
    //
    // Pass 3 — OFFSET / LIMIT removal
    //   Pagination clauses are meaningless for a total-count query.
    //   \S+ matches the parameter placeholder (@offset) or a literal integer.
    private static readonly Regex SelectClauseRegex = new(
        @"^\s*SELECT\s+.+?\s+FROM\s+",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex OrderByRegex = new(
        @"\s+ORDER\s+BY\s+.+$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex OffsetLimitRegex = new(
        @"\s+OFFSET\s+\S+\s+LIMIT\s+\S+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Converts the specified <see cref="SqlQuery"/> into a <see cref="QueryDefinition"/> suitable
    /// for use with the Azure Cosmos DB SDK, applying both the SQL text and any parameters.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown if the specification type is not supported.</exception>
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

    /// <summary>
    /// Derives a <c>SELECT VALUE COUNT(1)</c> query from an existing specification by rewriting
    /// its SQL text.  All original parameters are preserved.
    /// </summary>
    /// <remarks>
    /// The rewrite performs three passes:
    /// <list type="number">
    ///   <item>Replace the entire SELECT projection with <c>SELECT VALUE COUNT(1)</c>.</item>
    ///   <item>Strip any <c>ORDER BY</c> clause (invalid in COUNT queries).</item>
    ///   <item>Strip any <c>OFFSET … LIMIT …</c> clause (pagination is irrelevant for a count).</item>
    /// </list>
    /// This handles the full realistic query surface for Cosmos SQL: <c>SELECT *</c>,
    /// named projections, <c>SELECT VALUE</c>, JOINs, WHERE clauses, and paginated queries.
    /// </remarks>
    /// <exception cref="NotSupportedException">Thrown if the specification type is not <see cref="SqlSpecification{T}"/>.</exception>
    public static QueryDefinition ConvertToCountQuery<T>(this ISpecification<T> spec)
    {
        if (spec is not SqlSpecification<T> sql)
            throw new NotSupportedException(
                $"Spec of type {spec.GetType().Name} is not supported.");

        // Pass 1: replace the SELECT projection with COUNT(1).
        var countQueryText = SelectClauseRegex.Replace(
            sql.QueryText, "SELECT VALUE COUNT(1) FROM ");

        // Pass 2: strip ORDER BY — invalid in a COUNT query.
        countQueryText = OrderByRegex.Replace(countQueryText, string.Empty);

        // Pass 3: strip OFFSET/LIMIT — pagination is irrelevant for a total count.
        countQueryText = OffsetLimitRegex.Replace(countQueryText, string.Empty);

        var countQuery = new QueryDefinition(countQueryText.Trim());

        if (sql.Parameters != null)
            foreach (var parameter in sql.Parameters)
                countQuery = countQuery.WithParameter(parameter.Key, parameter.Value);

        return countQuery;
    }
}