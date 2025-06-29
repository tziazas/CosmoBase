namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Provides the current user context for audit field population.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Gets the current user identifier for audit fields.
    /// </summary>
    /// <returns>The current user identifier, or null if not available.</returns>
    string? GetCurrentUser();
}