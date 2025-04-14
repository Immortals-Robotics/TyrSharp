namespace Tyr.Common.Config;

// Attributes to mark configurable types and fields
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ConfigurableAttribute(string? comment = null) : Attribute
{
    public string? Comment { get; } = comment;
}