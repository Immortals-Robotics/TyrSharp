using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Tyr.Common.Debug;

public static class DebugTypeRegistry
{
    private static readonly ConcurrentDictionary<Type, byte> Types = new();

    [ModuleInitializer]
    internal static void Initialize()
    {
        Register<Logging.Entry>();
        Register<Drawing.Command>();
        Register<Plotting.Command>();
    }

    public static void Register<T>() where T : struct, IEntry
    {
        Register(typeof(T));
    }

    public static void Register(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!type.IsValueType)
            throw new ArgumentException($"Debug entry type {type.FullName} must be a value type.", nameof(type));

        if (!typeof(IEntry).IsAssignableFrom(type))
            throw new ArgumentException($"Debug entry type {type.FullName} must implement {nameof(IEntry)}.", nameof(type));

        Types.TryAdd(type, 0);
    }

    public static IReadOnlyList<Type> GetRegisteredTypes()
    {
        return Types.Keys
            .OrderBy(static type => type.FullName ?? type.Name, StringComparer.Ordinal)
            .ToArray();
    }
}
