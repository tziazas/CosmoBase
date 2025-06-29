using System.Collections.Generic;
using System.Linq;
using CosmoBase.Abstractions.Configuration;
using Microsoft.Extensions.Options;

namespace CosmoBase.Core.Configuration;

public class CosmosConfigurationValidator
    : IValidateOptions<CosmosConfiguration>
{
    public ValidateOptionsResult Validate(string? name, CosmosConfiguration config)
    {
        var errors = new List<string>();

        if (!config.CosmosClientConfigurations.Any())
            errors.Add("You must configure at least one CosmosClient.");

        var dupes = config.CosmosClientConfigurations
            .GroupBy(c => c.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        if (dupes.Any())
            errors.Add($"Duplicate client names: {string.Join(",", dupes)}");

        foreach (var model in config.CosmosModelConfigurations)
        {
            if (!config.CosmosClientConfigurations.Any(c => c.Name == model.ReadCosmosClientConfigurationName))
                errors.Add($"Model '{model.ModelName}' has invalid ReadCosmosClientConfigurationName");
            if (!config.CosmosClientConfigurations.Any(c => c.Name == model.WriteCosmosClientConfigurationName))
                errors.Add($"Model '{model.ModelName}' has invalid WriteCosmosClientConfigurationName");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}