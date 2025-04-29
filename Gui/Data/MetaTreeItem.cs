namespace Tyr.Gui.Data;

public record MetaTreeItem(MetaTreeItem.ItemType Type, int Line, string? Expression) : IComparable<MetaTreeItem>
{
    public int CompareTo(MetaTreeItem? other) => Line.CompareTo(other?.Line);

    public enum ItemType
    {
        Log,
        Draw,
        Plot,
    }
}