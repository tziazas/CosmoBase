using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using CosmoBase.Abstractions.Interfaces;

namespace CosmoBase.Abstractions.Models;

/// <summary>
/// Represents the result of a bulk execute operation.
/// </summary>
/// <typeparam name="T">The document type.</typeparam>
public class BulkExecuteResult<T> where T : class, ICosmosDataModel
{
    /// <summary>
    /// Items that were successfully processed.
    /// </summary>
    public List<T> SuccessfulItems { get; set; } = new();

    /// <summary>
    /// Items that failed to process with details about the failure.
    /// </summary>
    public List<BulkItemFailure<T>> FailedItems { get; set; } = new();

    /// <summary>
    /// Total request units consumed across all batches.
    /// </summary>
    public double TotalRequestUnits { get; set; }

    /// <summary>
    /// Whether the entire bulk operation was successful (no failures).
    /// </summary>
    public bool IsSuccess => !FailedItems.Any();

    /// <summary>
    /// Success rate as a percentage.
    /// </summary>
    public double SuccessRate
    {
        get
        {
            var total = SuccessfulItems.Count + FailedItems.Count;
            return total == 0 ? 100.0 : (SuccessfulItems.Count / (double)total) * 100.0;
        }
    }
}

/// <summary>
/// Represents a failed item in a bulk operation.
/// </summary>
/// <typeparam name="T">The document type.</typeparam>
public class BulkItemFailure<T> where T : class, ICosmosDataModel
{
    /// <summary>
    /// The item that failed to process.
    /// </summary>
    public required T Item { get; set; }

    /// <summary>
    /// The HTTP status code of the failure (if available).
    /// </summary>
    public HttpStatusCode? StatusCode { get; set; }

    /// <summary>
    /// The error message describing the failure.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The exception that caused the failure (if any).
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Whether this failure might be retryable.
    /// </summary>
    public bool IsRetryable => StatusCode switch
    {
        HttpStatusCode.RequestTimeout => true,
        HttpStatusCode.TooManyRequests => true,
        HttpStatusCode.ServiceUnavailable => true,
        HttpStatusCode.InternalServerError => true,
        _ => false
    };
}

/// <summary>
/// Internal result for a single batch execution.
/// </summary>
public class BatchExecuteResult<T> where T : class, ICosmosDataModel
{
    public int BatchIndex { get; set; }
    public List<T> SuccessfulItems { get; set; } = new();
    public List<BulkItemFailure<T>> FailedItems { get; set; } = new();
    public double RequestUnits { get; set; }
}