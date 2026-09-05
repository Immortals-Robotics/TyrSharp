using Tomlet;
using Tomlet.Models;

namespace Tyr.Common.Config;

/// <summary>
/// Metadata and untyped access for one <c>[ConfigEntry]</c> property.
/// Instances are created by the source generator; see <see cref="ConfigEntry{T}"/>.
/// </summary>
public abstract class ConfigEntry
{
    public string Name { get; }
    public Type Type { get; }
    public string? Description { get; }
    public StorageType StorageType { get; }
    public bool Editable { get; }

    /// <summary>The configurable this entry belongs to. Assigned by <see cref="Configurable"/>.</summary>
    public Configurable Owner { get; internal set; } = null!;

    protected ConfigEntry(string name, Type type, StorageType storageType, bool editable, string? description)
    {
        Name = name;
        Type = type;
        StorageType = storageType;
        Editable = editable;
        Description = description;
    }

    /// <summary>The value the property had when the configurable was created, i.e. its initializer.</summary>
    public abstract object? DefaultValue { get; }

    /// <summary>
    /// Reads or writes the underlying property. Writing goes through the generated setter,
    /// which raises a change notification only when the value actually differs.
    /// </summary>
    public abstract object? Value { get; set; }

    /// <summary>
    /// Assigns the value and raises exactly one change notification, even when the new value
    /// equals the old one. Use this for values that were edited in place (lists, dictionaries, objects).
    /// </summary>
    public void Set(object? value)
    {
        using (Owner.BeginBatch())
        {
            Value = value;
            MarkChanged();
        }
    }

    /// <summary>Raises a change notification without assigning. Use after mutating a value in place.</summary>
    public void MarkChanged() => Owner.MarkChanged(StorageType);

    public void ResetToDefault() => Value = DefaultValue;

    /// <summary>Serializes the current value, or returns null when the value is null and cannot be represented.</summary>
    public TomlValue? ToToml()
    {
        var value = Value;
        if (value is null) return null;

        var toml = TomlMappers.ValueFrom(Type, value);
        toml.Comments.PrecedingComment = Description;

        if (toml is not TomlArray)
            toml.Comments.InlineComment = $"default: {DefaultValue}";

        return toml;
    }

    public void FromToml(TomlValue value)
    {
        Value = TomletMain.To(Type, value);
    }
}

/// <summary>
/// Typed access to one <c>[ConfigEntry]</c> property through generated getter/setter delegates.
/// The setter is the generated property setter, so it carries change detection and notification.
/// </summary>
public sealed class ConfigEntry<T> : ConfigEntry
{
    private readonly Func<T> _getter;
    private readonly Action<T> _setter;

    public ConfigEntry(
        string name,
        StorageType storageType,
        bool editable,
        string? description,
        T defaultValue,
        Func<T> getter,
        Action<T> setter)
        : base(name, typeof(T), storageType, editable, description)
    {
        Default = defaultValue;
        _getter = getter;
        _setter = setter;
    }

    public T Default { get; }

    public T TypedValue
    {
        get => _getter();
        set => _setter(value);
    }

    public override object? DefaultValue => Default;

    public override object? Value
    {
        get => _getter();
        set => _setter((T)value!);
    }
}
