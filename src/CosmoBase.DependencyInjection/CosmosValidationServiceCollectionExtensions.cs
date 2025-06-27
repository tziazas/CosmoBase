using CosmoBase.Abstractions.Interfaces;
using CosmoBase.Core.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace CosmoBase.DependencyInjection;

/// <summary>
/// Dependency injection extensions for Cosmos validation.
/// </summary>
public static class CosmosValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default Cosmos validator for the specified document type.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCosmosValidator<T>(this IServiceCollection services)
        where T : class, ICosmosDataModel, new()
    {
        services.AddSingleton<ICosmosValidator<T>, CosmosValidator<T>>();
        return services;
    }

    /// <summary>
    /// Registers a custom Cosmos validator for the specified document type.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <typeparam name="TValidator">The validator implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCosmosValidator<T, TValidator>(this IServiceCollection services)
        where T : class, ICosmosDataModel, new()
        where TValidator : class, ICosmosValidator<T>
    {
        services.AddSingleton<ICosmosValidator<T>, TValidator>();
        return services;
    }

    /// <summary>
    /// Registers Cosmos validators for multiple document types.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="documentTypes">The document types to register validators for.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCosmosValidators(this IServiceCollection services, params Type[] documentTypes)
    {
        foreach (var docType in documentTypes)
        {
            if (!docType.IsAssignableTo(typeof(ICosmosDataModel)))
            {
                throw new ArgumentException($"Type {docType.Name} does not implement ICosmosDataModel", nameof(documentTypes));
            }

            var validatorType = typeof(CosmosValidator<>).MakeGenericType(docType);
            var interfaceType = typeof(ICosmosValidator<>).MakeGenericType(docType);
            
            services.AddSingleton(interfaceType, validatorType);
        }

        return services;
    }
}

// Usage examples in your Startup.cs or Program.cs:

/*
// Register validators for specific types
services.AddCosmosValidator<UserDocument>();
services.AddCosmosValidator<OrderDocument>();

// Register multiple validators at once
services.AddCosmosValidators(typeof(UserDocument), typeof(OrderDocument), typeof(ProductDocument));

// Register custom validator
services.AddCosmosValidator<UserDocument, CustomUserValidator>();

// Register repositories (they will automatically get the validators injected)
services.AddSingleton<ICosmosRepository<UserDocument>, CosmosRepository<UserDocument>>();
*/