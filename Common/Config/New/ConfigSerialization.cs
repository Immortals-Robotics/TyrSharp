namespace Tyr.Common.Config.New;

public class ConfigSerialization
{
    public static Dictionary<string, object> ToTree(IEnumerable<Configurable> configurables)
    {
        var root = new Dictionary<string, object>();

        foreach (var configurable in configurables)
        {
            var nParts = configurable.Namespace.Split('.');
            var current = root;

            foreach (var part in nParts)
            {
                if (!current.TryGetValue(part, out var child))
                    current[part] = child = new Dictionary<string, object>();
                current = (Dictionary<string, object>)child!;
            }

            var entries = configurable
                .Entries()
                .ToDictionary(entry => entry.Name, entry => entry.Value);

            current[configurable.TypeName] = entries;
        }

        return root;
    }
}