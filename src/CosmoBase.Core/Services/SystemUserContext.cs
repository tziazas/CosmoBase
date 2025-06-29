using CosmoBase.Abstractions.Interfaces;

namespace CosmoBase.Core.Services;

/// <summary>
/// System user context for background services and system operations.
/// </summary>
public class SystemUserContext(string systemUserName = "System") : IUserContext
{
    public string? GetCurrentUser() => systemUserName;
}