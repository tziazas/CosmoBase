namespace CosmoBase.Abstractions.Exceptions;

/// <summary>
/// Base exception type for all CosmoBase errors.
/// </summary>
public class CosmoBaseException : Exception
{
    public CosmoBaseException() { }
    public CosmoBaseException(string message) : base(message) { }
    public CosmoBaseException(string message, Exception inner) 
        : base(message, inner) { }
}