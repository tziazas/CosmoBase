using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Azure.Cosmos;
using CosmoBase.Abstractions.Configuration;

namespace WebApi.Advanced.Services;

/// <summary>
/// Health check for Cosmos DB connectivity
/// </summary>
public class CosmosHealthCheck(
    IReadOnlyDictionary<string, CosmosClient> cosmosClients,
    CosmosConfiguration configuration,
    ILogger<CosmosHealthCheck> logger)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var healthData = new Dictionary<string, object>();
            var allHealthy = true;
            var issues = new List<string>();

            // Check each configured Cosmos client
            foreach (var clientConfig in configuration.CosmosClientConfigurations)
            {
                try
                {
                    if (!cosmosClients.TryGetValue(clientConfig.Name, out var client))
                    {
                        issues.Add($"Client '{clientConfig.Name}' not found in registered clients");
                        allHealthy = false;
                        continue;
                    }

                    // Test connectivity by reading account properties
                    var accountProperties = await client.ReadAccountAsync();
                    
                    healthData[$"client_{clientConfig.Name}_status"] = "healthy";
                    healthData[$"client_{clientConfig.Name}_endpoint"] = client.Endpoint.ToString();
                    healthData[$"client_{clientConfig.Name}_regions"] = accountProperties.ReadableRegions?.Count() ?? 0;

                    logger.LogDebug("Cosmos client {ClientName} health check passed", clientConfig.Name);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Cosmos client {ClientName} health check failed", clientConfig.Name);
                    healthData[$"client_{clientConfig.Name}_status"] = "unhealthy";
                    healthData[$"client_{clientConfig.Name}_error"] = ex.Message;
                    issues.Add($"Client '{clientConfig.Name}': {ex.Message}");
                    allHealthy = false;
                }
            }

            // Check database and container existence for each model
            foreach (var modelConfig in configuration.CosmosModelConfigurations)
            {
                try
                {
                    if (!cosmosClients.TryGetValue(modelConfig.ReadCosmosClientConfigurationName, out var readClient))
                    {
                        issues.Add($"Read client '{modelConfig.ReadCosmosClientConfigurationName}' not found for model {modelConfig.ModelName}");
                        allHealthy = false;
                        continue;
                    }

                    // Check if database exists
                    var database = readClient.GetDatabase(modelConfig.DatabaseName);
                    var databaseResponse = await database.ReadAsync(null, cancellationToken);
                    
                    // Check if container exists
                    var container = database.GetContainer(modelConfig.CollectionName);
                    var containerResponse = await container.ReadContainerAsync(null, cancellationToken);
                    
                    healthData[$"model_{modelConfig.ModelName}_database"] = "exists";
                    healthData[$"model_{modelConfig.ModelName}_container"] = "exists";
                    healthData[$"model_{modelConfig.ModelName}_partition_key"] = containerResponse.Resource.PartitionKeyPath;

                    logger.LogDebug("Model {ModelName} health check passed", modelConfig.ModelName);
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    logger.LogWarning("Database or container not found for model {ModelName}: {Error}", modelConfig.ModelName, ex.Message);
                    healthData[$"model_{modelConfig.ModelName}_status"] = "not_found";
                    healthData[$"model_{modelConfig.ModelName}_error"] = ex.Message;
                    issues.Add($"Model '{modelConfig.ModelName}': Database or container not found");
                    allHealthy = false;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Model {ModelName} health check failed", modelConfig.ModelName);
                    healthData[$"model_{modelConfig.ModelName}_status"] = "unhealthy";
                    healthData[$"model_{modelConfig.ModelName}_error"] = ex.Message;
                    issues.Add($"Model '{modelConfig.ModelName}': {ex.Message}");
                    allHealthy = false;
                }
            }

            // Add summary information
            healthData["total_clients"] = configuration.CosmosClientConfigurations.Count;
            healthData["total_models"] = configuration.CosmosModelConfigurations.Count;
            healthData["check_timestamp"] = DateTime.UtcNow;

            if (allHealthy)
            {
                logger.LogInformation("Cosmos DB health check passed for all {ClientCount} clients and {ModelCount} models", 
                    configuration.CosmosClientConfigurations.Count, 
                    configuration.CosmosModelConfigurations.Count);

                return HealthCheckResult.Healthy(
                    "All Cosmos DB clients and models are healthy", 
                    healthData);
            }
            else
            {
                var issuesSummary = string.Join("; ", issues);
                logger.LogWarning("Cosmos DB health check failed: {Issues}", issuesSummary);

                return HealthCheckResult.Degraded(
                    $"Some Cosmos DB resources are unhealthy: {issuesSummary}", 
                    data: healthData);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cosmos DB health check encountered an unexpected error");
            
            return HealthCheckResult.Unhealthy(
                "Cosmos DB health check failed with unexpected error", 
                ex, 
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["check_timestamp"] = DateTime.UtcNow
                });
        }
    }
}