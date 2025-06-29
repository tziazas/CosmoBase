using System;

namespace CosmoBase.Abstractions.Exceptions;

/// <summary>
/// Exception type for all CosmoBase configuration errors.
/// </summary>
public class CosmosConfigurationException : Exception
{
    public CosmosConfigurationException() { }
    public CosmosConfigurationException(string message) : base(message) { }
    public CosmosConfigurationException(string message, Exception inner) 
        : base(message, inner) { }
}