using Tyr.Common.Debug;
using Tyr.Common.Debug.Db;
using Tyr.Common.Time;

namespace Tyr.Gui.Data;

public sealed class SwitchableDebugDb(DebugDb source)
{
    private DebugDb _source = source;

    public void SetSource(DebugDb source)
    {
        Interlocked.Exchange(ref _source, source);
    }

    public void Append<T>(T entry) where T : struct, IEntry
        => _source.Append(entry);

    public IEnumerable<T> Query<T>(string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
        => _source.Query<T>(module, t0, t1, shardKey, maxCount);

    public IEnumerable<T> QueryWithoutMeta<T>(string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
        => _source.QueryWithoutMeta<T>(module, t0, t1, shardKey, maxCount);

    public IEnumerable<T> Query<T>(Timestamp t0, Timestamp t1, string? module = null, int? sourceLocationId = null, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
        => _source.Query<T>(t0, t1, module, sourceLocationId, shardKey, maxCount);

    public IEnumerable<T> QueryAll<T>(Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry
        => _source.QueryAll<T>(t0, t1, shardKey, maxCount);

    public IEnumerable<string> QueryModules() => _source.QueryModules();

    public IEnumerable<string> QueryShardKeys<T>(string module) where T : struct, IEntry
        => _source.QueryShardKeys<T>(module);

    public IEnumerable<Meta> QuerySourceLocations<T>(string module) where T : struct, IEntry
        => _source.QuerySourceLocations<T>(module);

    public Meta? TryGetShardMeta<T>(string module, string shardKey) where T : struct, IEntry
        => _source.TryGetShardMeta<T>(module, shardKey);

    public Meta GetSourceLocation(int id) => _source.GetSourceLocation(id);

    public void AppendFrame(Frame frame) => _source.AppendFrame(frame);

    public (Timestamp Start, Timestamp End)? GetFrameRange() => _source.GetFrameRange();

    public IEnumerable<Frame> QueryFrames(string module, Timestamp t0, Timestamp t1)
        => _source.QueryFrames(module, t0, t1);

    public (Timestamp Start, Timestamp End)? GetFrameAt(string module, Timestamp t)
        => _source.GetFrameAt(module, t);
}
