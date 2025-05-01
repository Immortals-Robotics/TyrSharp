namespace Tyr.Common.Config;

[AttributeUsage(AttributeTargets.Property)]
public class ConfigEntryAttribute(string? description = null, StorageType storageType = StorageType.Project)
    : Attribute
{
    public ConfigEntryAttribute(StorageType storageType) : this(null, storageType)
    {
    }

    public string? Description { get; } = description;
    public StorageType StorageType { get; } = storageType;
}