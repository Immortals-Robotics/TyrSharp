namespace Tyr.Common.Config.New;

[AttributeUsage(AttributeTargets.Property)]
public class ConfigEntryAttribute(string comment = "") : Attribute
{
    public string Comment { get; } = comment;
}