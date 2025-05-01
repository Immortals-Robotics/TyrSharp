namespace Tyr.Common.Debug;

public static class EnumCache<T> where T : Enum
{
    public static T[] Values { get; } = (T[])Enum.GetValues(typeof(T));
    public static string[] Names { get; } = Enum.GetNames(typeof(T));

    private static readonly Dictionary<T, string> NameLookup;

    static EnumCache()
    {
        NameLookup = new Dictionary<T, string>(Values.Length);

        for (var index = 0; index < Values.Length; index++)
        {
            var value = Values[index];
            var name = Names[index];
            NameLookup[value] = name;
        }
    }

    public static string GetName(T value) => NameLookup[value];

    public static int GetIndex(T value) => Array.IndexOf(Values, value);
    public static T GetByIndex(int index) => Values[index];
}