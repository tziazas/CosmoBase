using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace CosmoBase.Core.Configuration;

/// <summary>
/// Validates CosmoBase configuration options.
/// </summary>
public class CosmoBaseOptionsValidator : IValidateOptions<CosmoBaseOptions>
{
    private static readonly Regex ConnectionStringPattern = new Regex(
        @"AccountEndpoint=https?://[^;]+;AccountKey=[^;]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ValidateOptionsResult Validate(string? name, CosmoBaseOptions options)
    {
        // if (options == null)
        // {
        //     return ValidateOptionsResult.Fail("Options cannot be null");
        // }

        // Validate connection string
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("ConnectionString is required");
        }

        if (!ConnectionStringPattern.IsMatch(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail(
                "ConnectionString format is invalid. Expected format: AccountEndpoint=https://...;AccountKey=...");
        }

        // Validate database name
        if (string.IsNullOrWhiteSpace(options.DatabaseName))
        {
            return ValidateOptionsResult.Fail("DatabaseName is required");
        }

        if (options.DatabaseName.Length > 255)
        {
            return ValidateOptionsResult.Fail("DatabaseName cannot exceed 255 characters");
        }

        // Validate retry settings
        if (options.MaxRetryAttempts < 0 || options.MaxRetryAttempts > 10)
        {
            return ValidateOptionsResult.Fail("MaxRetryAttempts must be between 0 and 10");
        }

        if (options.MaxRetryWaitTimeInSeconds < 1 || options.MaxRetryWaitTimeInSeconds > 300)
        {
            return ValidateOptionsResult.Fail("MaxRetryWaitTimeInSeconds must be between 1 and 300");
        }

        // Validate bulk operation settings
        if (options.BulkOperationBatchSize < 1 || options.BulkOperationBatchSize > 1000)
        {
            return ValidateOptionsResult.Fail("BulkOperationBatchSize must be between 1 and 1000");
        }

        // Validate throughput settings
        if (options.Throughput.AutoscaleEnabled && options.Throughput.AutoscaleMaxThroughput.HasValue)
        {
            if (options.Throughput.AutoscaleMaxThroughput.Value < 1000)
            {
                return ValidateOptionsResult.Fail("AutoscaleMaxThroughput must be at least 1000 RU/s");
            }

            if (options.Throughput.AutoscaleMaxThroughput.Value % 1000 != 0)
            {
                return ValidateOptionsResult.Fail("AutoscaleMaxThroughput must be in increments of 1000");
            }
        }

        // Validate container configurations
        foreach (var (containerName, config) in options.ContainerConfigurations)
        {
            if (string.IsNullOrWhiteSpace(config.ContainerName))
            {
                return ValidateOptionsResult.Fail(
                    $"Container name is required for configuration key '{containerName}'");
            }

            if (string.IsNullOrWhiteSpace(config.PartitionKeyPath))
            {
                return ValidateOptionsResult.Fail(
                    $"PartitionKeyPath is required for container '{config.ContainerName}'");
            }

            if (!config.PartitionKeyPath.StartsWith("/"))
            {
                return ValidateOptionsResult.Fail(
                    $"PartitionKeyPath must start with '/' for container '{config.ContainerName}'");
            }

            if (config.Throughput.HasValue && config.Throughput.Value < 400)
            {
                return ValidateOptionsResult.Fail(
                    $"Container throughput must be at least 400 RU/s for container '{config.ContainerName}'");
            }
        }

        return ValidateOptionsResult.Success;
    }
}