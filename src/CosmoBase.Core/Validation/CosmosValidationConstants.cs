namespace CosmoBase.Core.Validation;

/// <summary>
/// Constants used for Cosmos DB validation.
/// </summary>
internal static class CosmosValidationConstants
{
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