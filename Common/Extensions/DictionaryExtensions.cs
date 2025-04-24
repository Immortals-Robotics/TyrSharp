namespace Tyr.Common.Extensions;

public static class DictionaryExtensions
{
    public static void RemoveAll<TKey, TValue>(this Dictionary<TKey, TValue> dict,
        Func<TKey, TValue, bool> predicate) where TKey : notnull
    {
        var keys = dict.Keys
            .Where(k => predicate(k, dict[k]))
            .ToList();
        
        foreach (var key in keys)
            dict.Remove(key);
    }
}