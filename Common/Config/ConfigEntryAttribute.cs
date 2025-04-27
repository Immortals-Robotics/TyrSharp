namespace Tyr.Common.Config;

[AttributeUsage(AttributeTargets.Property)]
public class ConfigEntryAttribute(string? description = null) : Attribute
{
    public string? Description { get; } = description;
}