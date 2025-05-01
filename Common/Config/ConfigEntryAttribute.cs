namespace Tyr.Common.Config;

[AttributeUsage(AttributeTargets.Property)]
public class ConfigEntryAttribute(
    string? description = null,
    StorageType storageType = StorageType.Project,
    bool editable = true)
    : Attribute
{
    public ConfigEntryAttribute(StorageType storageType, bool editable = true)
        : this(null, storageType, editable)
    {
    }

    public string? Description { get; } = description;
    public StorageType StorageType { get; } = storageType;
    public bool Editable { get; set; } = editable;
}