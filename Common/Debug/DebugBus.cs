using Tyr.Common.Debug.Db;

namespace Tyr.Common.Debug;

public static class DebugBus
{
    private static volatile DebugDb? _db;

    public static void SetDb(DebugDb? db) => _db = db;

    public static void Publish<T>(T entry) where T : struct, IEntry
    {
        DebugTypeRegistry.Register<T>();
        _db?.Append(entry);
    }

    public static void AppendFrame(Frame frame)
    {
        _db?.AppendFrame(frame);
    }
}
