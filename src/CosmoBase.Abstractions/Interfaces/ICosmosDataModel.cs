using System;
using System.Text.Json.Serialization;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// Defines the common metadata that every Cosmos DB document must carry.
/// This ensures uniform handling of identity, auditing, and soft‐deletion
/// across all repositories (see <see cref="ICosmosRepository{T}"/>).
/// </summary>
public interface ICosmosDataModel
{
    /// <summary>
    /// The unique identifier of the document.
    /// This maps to the “id” property in Cosmos DB.
    /// </summary>
    [JsonPropertyName("id")]
    string Id { get; set; }

    /// <summary>
    /// The UTC timestamp when the document was first created.
    /// </summary>
    DateTime? CreatedOnUtc { get; set; }

    /// <summary>
    /// The UTC timestamp of the most recent update to the document.
    /// </summary>
    DateTime? UpdatedOnUtc { get; set; }

    /// <summary>
    /// The user or system principal that originally created the document.
    /// </summary>
    string? CreatedBy { get; set; }

    /// <summary>
    /// The user or system principal that last modified the document.
    /// </summary>
    string? UpdatedBy { get; set; }

    /// <summary>
    /// Soft‐delete flag. When <c>true</c>, the document is treated as deleted
    /// without physically removing it from the container.
    /// </summary>
    bool Deleted { get; set; }
}