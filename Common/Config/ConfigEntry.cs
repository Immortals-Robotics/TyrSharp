using System.Reflection;
using Tomlet;
using Tomlet.Models;

namespace Tyr.Common.Config;

public class ConfigEntry(PropertyInfo info, Configurable owner)
{
    private readonly ConfigEntryAttribute _meta = info.GetCustomAttribute<ConfigEntryAttribute>()!;

    public string Name => info.Name;
    public Type Type => info.PropertyType;
    public object DefaultValue { get; } = info.GetValue(null)!;

    public string? Description => _meta.Description;
    public StorageType StorageType => _meta.StorageType;
    public bool Editable => _meta.Editable;

    static ConfigEntry()
    {
        TomletMain.RegisterMapper<ConfigEntry>(entry => entry?.ToToml(), null);
    }

    public object Value
    {
        get => info.GetValue(null)!;
        set
        {
            var current = info.GetValue(null);
            if (Equals(current, value)) return;

            info.SetValue(null, value);
            owner.MarkChanged(StorageType);
        }
    }

    public TomlValue ToToml()
    {
        var value = TomletMain.ValueFrom(Type, Value);
        value.Comments.PrecedingComment = Description;

        if (value is not TomlArray)
            value.Comments.InlineComment = $"default: {DefaultValue}";

        return value;
    }

    public void FromToml(TomlValue value)
    {
        Value = TomletMain.To(Type, value);
    }
}