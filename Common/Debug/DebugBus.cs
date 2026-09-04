using Tyr.Common.Dataflow;

namespace Tyr.Common.Debug;

/// <summary>
/// One broadcast channel per debug entry type. The channel for a type is a generic static,
/// so publishing costs no dictionary lookup, and touching it registers the type with
/// <see cref="DebugTypeRegistry"/> exactly once.
/// </summary>
public static class DebugBus
{
    private static class Channel<T> where T : struct, IEntry
    {
        public static readonly BroadcastChannel<T> Instance = new();

        static Channel()
        {
            DebugTypeRegistry.Register<T>();
        }
    }

    public static void Publish<T>(T entry) where T : struct, IEntry
    {
        Channel<T>.Instance.Publish(entry);
    }

    public static Subscriber<T> Subscribe<T>(Mode mode) where T : struct, IEntry
    {
        return Channel<T>.Instance.Subscribe(mode);
    }

    public static void PublishFrame(Frame frame)
    {
        Hub.Frames.Publish(frame);
    }

    public static Subscriber<Frame> SubscribeFrames(Mode mode)
    {
        return Hub.Frames.Subscribe(mode);
    }
}
