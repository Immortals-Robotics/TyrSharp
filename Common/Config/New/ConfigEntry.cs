using System.Reflection;
using Tomlet;
using Tomlet.Models;

namespace Tyr.Common.Config.New;

public class ConfigEntry(MemberInfo memberInfo, Configurable owner)
{
    private readonly PropertyInfo _info = (PropertyInfo)memberInfo;
    private readonly ConfigEntryAttribute _meta = memberInfo.GetCustomAttribute<ConfigEntryAttribute>()!;

    public string Name => _info.Name;
    public Type Type => _info.PropertyType;
    public string? Comment => _meta.Comment;
    public object DefaultValue { get; } = ((PropertyInfo)memberInfo).GetValue(null)!;

    static ConfigEntry()
    {
        TomletMain.RegisterMapper<ConfigEntry>(entry => entry?.ToToml(), null);
    }

    public object Value
    {
        get => _info.GetValue(null)!;
        set
        {
            var converted = Convert.ChangeType(value, Type);

            var current = _info.GetValue(null);
            if (Equals(current, converted)) return;

            _info.SetValue(null, converted);
            owner.OnEntryChanged(this);
        }
    }

    public TomlValue ToToml()
    {
        var value = TomletMain.ValueFrom(Type, Value);
        value.Comments.PrecedingComment = Comment;
        value.Comments.InlineComment = $"default: {DefaultValue}";

        return value;
    }

    public void FromToml(TomlValue value)
    {
        Value = TomletMain.To(Type, value);
    }
}