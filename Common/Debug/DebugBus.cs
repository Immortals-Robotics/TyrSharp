using System.Collections.Concurrent;
using Tyr.Common.Dataflow;

namespace Tyr.Common.Debug;

public static class DebugBus
{
    private static readonly ConcurrentDictionary<Type, object> Channels = new();
    private static readonly BroadcastChannel<DebugDumpEntry> DumpChannel = new();

    public static void Publish<T>(T entry) where T : struct, IEntry
    {
        DebugTypeRegistry.Register<T>();
        GetChannel<T>().Publish(entry);
        DumpChannel.Publish(DebugDumpEntry.From(entry));
    }

    public static Subscriber<T> Subscribe<T>(Mode mode) where T : struct, IEntry
    {
        DebugTypeRegistry.Register<T>();
        return GetChannel<T>().Subscribe(mode);
    }

    public static void PublishFrame(Frame frame)
    {
        Hub.Frames.Publish(frame);
        DumpChannel.Publish(DebugDumpEntry.From(frame));
    }

    public static Subscriber<DebugDumpEntry> SubscribeDumpEntries(Mode mode)
    {
        return DumpChannel.Subscribe(mode);
    }

    private static BroadcastChannel<T> GetChannel<T>() where T : struct, IEntry
    {
        return (BroadcastChannel<T>)Channels.GetOrAdd(typeof(T), static _ => new BroadcastChannel<T>());
    }
}
