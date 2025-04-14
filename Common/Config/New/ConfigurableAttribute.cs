namespace Tyr.Common.Config.New;

// Attributes to mark configurable types and fields
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ConfigurableAttribute(string? comment = null) : Attribute
{
    public string? Comment { get; } = comment;
}