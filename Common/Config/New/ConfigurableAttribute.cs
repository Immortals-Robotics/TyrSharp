namespace Tyr.Common.Config.New;

// Attributes to mark configurable types and fields
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ConfigurableAttribute : Attribute
{
}