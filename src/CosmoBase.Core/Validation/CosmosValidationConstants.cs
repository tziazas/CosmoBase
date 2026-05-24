using System;
using System.Text.RegularExpressions;

namespace CosmoBase.Core.Validation;

/// <summary>
/// Constants used for Cosmos DB validation.
/// </summary>
internal static class CosmosValidationConstants
{
    /// <summary>
    /// Regex that matches safe Cosmos DB property identifier names.
    /// Allows letters, digits, underscores, and dots (for nested paths such as "order.items").
    /// Must start with a letter or underscore.
    /// </summary>
    public static readonly Regex SafePropertyNamePattern =
        new(@"^[a-zA-Z_][a-zA-Z0-9_.]*$", RegexOptions.Compiled);
    /// <summary>
    /// Types supported for partition key properties.
    /// </summary>
    public static readonly Type[] SupportedPartitionKeyTypes =
    [
        typeof(string), typeof(int), typeof(long), typeof(double), typeof(bool)
    ];

    /// <summary>
    /// Characters that are invalid in Cosmos DB document IDs.
    /// </summary>
    public static readonly char[] InvalidIdCharacters = ['/', '\\', '?', '#'];

    /// <summary>
    /// Maximum allowed length for document IDs in Cosmos DB.
    /// </summary>
    public const int MaxDocumentIdLength = 255;

    /// <summary>
    /// Maximum recommended page size for queries.
    /// </summary>
    public const int MaxPageSize = 1000;

    /// <summary>
    /// Maximum recommended batch size for bulk operations.
    /// </summary>
    public const int MaxBatchSize = 100;

    /// <summary>
    /// Maximum recommended concurrency for bulk operations.
    /// </summary>
    public const int MaxConcurrency = 50;
}