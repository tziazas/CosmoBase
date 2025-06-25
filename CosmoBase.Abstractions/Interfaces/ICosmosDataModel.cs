using System.Text.Json.Serialization;

namespace CosmoBase.Abstractions.Interfaces;

/// <summary>
/// This interface contains the parameters we would like all
/// documents in Cosmos Db to have. We enforce using this data model
/// inside the repository. See ICosmosRepository.
/// </summary>
public interface ICosmosDataModel
{
    [JsonPropertyName("id")]
    string Id { get; set; }
    DateTime? CreatedDateTimeUtc { get; set; }
    DateTime? UpdatedDateTimeUtc { get; set; }
    string? WhoCreated { get; set; }
    string? WhoUpdated { get; set; }
    bool IsDeleted { get; set; }
}