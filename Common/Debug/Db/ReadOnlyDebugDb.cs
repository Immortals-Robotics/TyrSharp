using Tyr.Common.Debug;
using Tyr.Common.Time;

namespace Tyr.Common.Debug.Db;

public sealed class ReadOnlyDebugDb(IDebugDb inner) : IDebugDb
{
    public void Append<T>(T entry) where T : struct, IEntry
        => throw new InvalidOperationException("Cannot append to a read-only debug database.");

    public IEnumerable<T> Query<T>(string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
        => inner.Query<T>(module, t0, t1, shardKey, maxCount);

    public IEnumerable<T> QueryWithoutMeta<T>(string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
        => inner.QueryWithoutMeta<T>(module, t0, t1, shardKey, maxCount);

    public IEnumerable<T> Query<T>(Timestamp t0, Timestamp t1, string? module = null, int? sourceLocationId = null, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
        => inner.Query<T>(t0, t1, module, sourceLocationId, shardKey, maxCount);

    public IEnumerable<T> QueryAll<T>(Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
        => inner.QueryAll<T>(t0, t1, shardKey, maxCount);

    public IEnumerable<string> QueryModules() => inner.QueryModules();

    public IEnumerable<string> QueryShardKeys<T>(string module) where T : struct, IEntry
        => inner.QueryShardKeys<T>(module);

    public IEnumerable<Meta> QuerySourceLocations<T>(string module) where T : struct, IEntry
        => inner.QuerySourceLocations<T>(module);

    public Meta? TryGetShardMeta<T>(string module, string shardKey) where T : struct, IEntry
        => inner.TryGetShardMeta<T>(module, shardKey);

    public Meta GetSourceLocation(int id) => inner.GetSourceLocation(id);

    public void AppendFrame(Frame frame)
        => throw new InvalidOperationException("Cannot append frames to a read-only debug database.");

    public (Timestamp Start, Timestamp End)? GetFrameRange() => inner.GetFrameRange();

    public IEnumerable<Frame> QueryFrames(string module, Timestamp t0, Timestamp t1)
        => inner.QueryFrames(module, t0, t1);

    public (Timestamp Start, Timestamp End)? GetFrameAt(string module, Timestamp t)
        => inner.GetFrameAt(module, t);

    public void Dispose() => inner.Dispose();
}
