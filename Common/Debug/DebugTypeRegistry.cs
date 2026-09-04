using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Tyr.Common.Debug.Drawing.Drawables;

namespace Tyr.Common.Debug;

/// <summary>
/// Set of every debug entry type seen by the process. Types register here from module
/// initializers (so a playback database can open their buckets) and from the first
/// publish/subscribe on <see cref="DebugBus"/>. <see cref="Registered"/> fires for types
/// that register after a listener attached, so a running dumper can start draining them.
/// </summary>
public static class DebugTypeRegistry
{
    private static readonly ConcurrentDictionary<Type, byte> Types = new();
    private static readonly Lock RegisteredLock = new();
    private static Action<Type>? _registered;

    /// <summary>
    /// Raised for newly registered types. Attaching a handler also replays every type
    /// registered so far, so a listener never misses one. A type that registers while a
    /// handler is being attached can be delivered twice; handlers must tolerate that.
    /// Handlers run outside any registry lock, so they may register or subscribe freely.
    /// </summary>
    public static event Action<Type> Registered
    {
        add
        {
            lock (RegisteredLock)
                _registered += value;

            foreach (var type in GetRegisteredTypes())
                value(type);
        }
        remove
        {
            lock (RegisteredLock)
                _registered -= value;
        }
    }

#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should only be used in
    [ModuleInitializer]
    internal static void Initialize()
    {
        Register<Logging.Entry>();
        Register<Circle>();
        Register<Arc>();
        Register<Arrow>();
        Register<Line>();
        Register<LineSegment>();
        Register<Drawing.Drawables.Path>();
        Register<Point>();
        Register<Rectangle>();
        Register<Robot>();
        Register<Text>();
        Register<Triangle>();
        Register<Plotting.Command>();
    }
#pragma warning restore CA2255

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

        if (!Types.TryAdd(type, 0))
            return;

        Volatile.Read(ref _registered)?.Invoke(type);
    }

    public static IReadOnlyList<Type> GetRegisteredTypes()
    {
        return Types.Keys
            .OrderBy(static type => type.FullName ?? type.Name, StringComparer.Ordinal)
            .ToArray();
    }
}
