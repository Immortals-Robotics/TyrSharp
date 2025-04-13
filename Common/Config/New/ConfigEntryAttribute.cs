namespace Tyr.Common.Config.New;

[AttributeUsage(AttributeTargets.Property)]
public class ConfigEntryAttribute(object defaultValue, string comment = "") : Attribute
{
    public object DefaultValue { get; } = defaultValue;
    public string Comment { get; } = comment;
}