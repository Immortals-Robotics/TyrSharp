using System.Reflection;

namespace Tyr.Common.Config.New;

public class ConfigEntry(MemberInfo memberInfo, Configurable owner)
{
    private readonly PropertyInfo _info = (PropertyInfo)memberInfo;
    private readonly ConfigEntryAttribute _meta = memberInfo.GetCustomAttribute<ConfigEntryAttribute>()!;

    public string Name => _info.Name;
    public Type Type => _info.PropertyType;

    public string Comment => _meta.Comment;
    public object DefaultValue => _meta.DefaultValue;

    public object Value
    {
        get => _info.GetValue(null)!;
        set
        {
            if (Value == value)
                return;

            _info.SetValue(null, Convert.ChangeType(value, Type));
            owner.OnEntryChanged(this);
        }
    }
}