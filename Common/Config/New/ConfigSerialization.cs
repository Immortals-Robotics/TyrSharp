using Tomlyn.Helpers;

namespace Tyr.Common.Config.New;

public class ConfigSerialization
{
    public static readonly Func<string, string> ConvertName = TomlNamingHelper.PascalToSnakeCase;

    // Converts a list of Configurable into a nested tree
    public static IDictionary<string, object> ToTree(IEnumerable<Configurable> configurables)
    {
        var root = new Dictionary<string, object>();

        foreach (var configurable in configurables)
        {
            var namespaceParts = configurable.Namespace.Split('.').Skip(1);
            var current = root;

            foreach (var namespacePart in namespaceParts)
            {
                var convertedName = ConvertName(namespacePart);
                if (!current.TryGetValue(convertedName, out var child))
                    current[convertedName] = child = new Dictionary<string, object>();
                current = (Dictionary<string, object>)child!;
            }

            var entries = configurable
                .Entries()
                .ToDictionary(entry => ConvertName(entry.Name), entry => entry.Value);

            current[ConvertName(configurable.TypeName)] = entries;
        }

        return root;
    }
}