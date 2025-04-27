namespace Tyr.Common.Config;

// Attributes to mark configurable types and fields
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ConfigurableAttribute(string? description = null) : Attribute
{
    public string? Description { get; } = description;
}