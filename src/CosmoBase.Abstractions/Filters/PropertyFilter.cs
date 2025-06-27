namespace CosmoBase.Abstractions.Filters;

public class PropertyFilter
{
    public string PropertyName { get; set; } = string.Empty;
    public object PropertyValue { get; set; } = string.Empty;
    public string PropertyComparison { get; set; } = string.Empty;
}