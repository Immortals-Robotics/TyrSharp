namespace Tyr.Common.Config;

[AttributeUsage(AttributeTargets.Property)]
public class ConfigEntryAttribute(string? comment = null) : Attribute
{
    public string? Comment { get; } = comment;
}