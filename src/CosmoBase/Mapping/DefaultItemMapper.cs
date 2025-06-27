using System.Text.Json;
using CosmoBase.Abstractions.Interfaces;

namespace CosmoBase.Mapping;

/// <summary>
/// A very generic mapper that serializes the source to JSON
/// then deserializes to the target type. Handy as a “no-frills” default.
/// </summary>
public class DefaultItemMapper<TDao, TDto> : IItemMapper<TDao, TDto>
    where TDao : class, new()
    where TDto : class, new()
{
    private static readonly JsonSerializerOptions _opts = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public TDao ToDao(TDto dto)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));
        var json = JsonSerializer.Serialize(dto, _opts);
        return JsonSerializer.Deserialize<TDao>(json, _opts)
               ?? throw new InvalidOperationException(
                   $"Deserializing DTO→DAO resulted in null for {typeof(TDao).Name}");
    }

    public TDto FromDao(TDao dao)
    {
        if (dao is null) throw new ArgumentNullException(nameof(dao));
        var json = JsonSerializer.Serialize(dao, _opts);
        return JsonSerializer.Deserialize<TDto>(json, _opts)
               ?? throw new InvalidOperationException(
                   $"Deserializing DAO→DTO resulted in null for {typeof(TDto).Name}");
    }
}