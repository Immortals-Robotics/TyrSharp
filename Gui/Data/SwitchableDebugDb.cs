using Tyr.Common.Debug.Db;
using Tyr.Common.Time;

namespace Tyr.Gui.Data;

internal sealed class SwitchableDebugDb(IDebugDb source) : IDebugDb
{
    private IDebugDb _source = source;

    public void SetSource(IDebugDb source)
    {
        Interlocked.Exchange(ref _source, source);
    }

    void IDebugDb.Append<T>(T entry)
        => _source.Append(entry);

    IEnumerable<T> IDebugDb.Query<T>(string module, Timestamp t0, Timestamp t1, string? shardKey, int? maxCount)
        => _source.Query<T>(module, t0, t1, shardKey, maxCount);

    IEnumerable<T> IDebugDb.QueryWithoutMeta<T>(string module, Timestamp t0, Timestamp t1, string? shardKey, int? maxCount)
        => _source.QueryWithoutMeta<T>(module, t0, t1, shardKey, maxCount);

    IEnumerable<T> IDebugDb.Query<T>(Timestamp t0, Timestamp t1, string? module, int? sourceLocationId, string? shardKey, int? maxCount)
        => _source.Query<T>(t0, t1, module, sourceLocationId, shardKey, maxCount);

    IEnumerable<T> IDebugDb.QueryAll<T>(Timestamp t0, Timestamp t1, string? shardKey, int? maxCount)
        => _source.QueryAll<T>(t0, t1, shardKey, maxCount);

    IEnumerable<string> IDebugDb.QueryModules() => _source.QueryModules();

    IEnumerable<string> IDebugDb.QueryShardKeys<T>(string module)
        => _source.QueryShardKeys<T>(module);

    IEnumerable<Tyr.Common.Debug.Meta> IDebugDb.QuerySourceLocations<T>(string module)
        => _source.QuerySourceLocations<T>(module);
    
    IEnumerable<Tyr.Common.Debug.Meta> IDebugDb.QuerySourceLocations(string module, Type type)
        => _source.QuerySourceLocations(module, type);

    Tyr.Common.Debug.Meta? IDebugDb.TryGetShardMeta<T>(string module, string shardKey)
        => _source.TryGetShardMeta<T>(module, shardKey);

    Tyr.Common.Debug.Meta IDebugDb.GetSourceLocation(int id) => _source.GetSourceLocation(id);

    void IDebugDb.AppendFrame(Tyr.Common.Debug.Frame frame) => _source.AppendFrame(frame);

    (Timestamp Start, Timestamp End)? IDebugDb.GetFrameRange() => _source.GetFrameRange();

    IEnumerable<Tyr.Common.Debug.Frame> IDebugDb.QueryFrames(string module, Timestamp t0, Timestamp t1)
        => _source.QueryFrames(module, t0, t1);

    (Timestamp Start, Timestamp End)? IDebugDb.GetFrameAt(string module, Timestamp t)
        => _source.GetFrameAt(module, t);

    void IDisposable.Dispose()
    {
    }
}
