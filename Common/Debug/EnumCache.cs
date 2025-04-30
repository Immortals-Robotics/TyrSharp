namespace Tyr.Common.Debug;

public static class EnumCache<T> where T : Enum
{
    private static readonly Dictionary<T, string> NameLookup;

    static EnumCache()
    {
        var values = Enum.GetValues(typeof(T));
        NameLookup = new Dictionary<T, string>(values.Length);

        foreach (T value in values)
        {
            NameLookup[value] = string.Intern(value.ToString());
        }
    }

    public static string GetName(T value)
    {
        return NameLookup[value];
    }
}