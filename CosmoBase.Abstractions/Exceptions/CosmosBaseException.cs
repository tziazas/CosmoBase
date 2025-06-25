namespace CosmoBase.Abstractions.Exceptions;

/// <summary>
/// Base exception type for all CosmoBase errors.
/// </summary>
public class CosmosBaseException : Exception
{
    public CosmosBaseException() { }
    public CosmosBaseException(string message) : base(message) { }
    public CosmosBaseException(string message, Exception inner) 
        : base(message, inner) { }
}