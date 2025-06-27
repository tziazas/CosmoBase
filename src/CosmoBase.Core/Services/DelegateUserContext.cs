using System;
using CosmoBase.Abstractions.Interfaces;

namespace CosmoBase.Core.Services;

/// <summary>
/// User context implementation that uses a delegate function to resolve the current user.
/// </summary>
public class DelegateUserContext : IUserContext
{
    private readonly Func<string> _userProvider;

    /// <summary>
    /// Initializes a new instance with the specified user provider function.
    /// </summary>
    /// <param name="userProvider">A function that returns the current user identifier.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userProvider"/> is null.</exception>
    public DelegateUserContext(Func<string?> userProvider)
    {
        _userProvider = userProvider ?? throw new ArgumentNullException(nameof(userProvider));
    }

    /// <inheritdoc/>
    public string? GetCurrentUser()
    {
        try
        {
            return _userProvider();
        }
        catch (Exception)
        {
            // If user provider throws, fall back to system user
            // This prevents audit field updates from failing due to user resolution issues
            return "System";
        }
    }
}