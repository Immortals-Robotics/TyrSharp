namespace Tyr.Common.Config;

/// <summary>
/// A node of the namespace tree built from all registered configurables.
/// Leaves carry a <see cref="Configurable"/>; inner nodes are namespace segments.
/// </summary>
public sealed class ConfigTreeNode(string name)
{
    public string Name { get; } = name;
    public Configurable? Configurable { get; internal set; }
    public SortedDictionary<string, ConfigTreeNode> Children { get; } = new(StringComparer.Ordinal);

    public bool IsLeaf => Configurable is not null;
}
