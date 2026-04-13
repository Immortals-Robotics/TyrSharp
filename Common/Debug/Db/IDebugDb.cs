namespace Tyr.Common.Debug.Db;

public interface IDebugDb : IDisposable
{
    void Append<T>(T entry) where T : struct, IEntry;
    IEnumerable<T> Query<T>(string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry;
    IEnumerable<T> QueryWithoutMeta<T>(string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry;
    IEnumerable<T> Query<T>(Timestamp t0, Timestamp t1, string? module = null, int? sourceLocationId = null, string? shardKey = null, int? maxCount = null) where T : struct, IEntry;
    IEnumerable<T> QueryAll<T>(Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry;
    IEnumerable<string> QueryModules();
    IEnumerable<string> QueryShardKeys<T>(string module) where T : struct, IEntry;
    IEnumerable<Meta> QuerySourceLocations<T>(string module) where T : struct, IEntry;
    IEnumerable<Meta> QuerySourceLocations(string module, Type type);
    Meta? TryGetShardMeta<T>(string module, string shardKey) where T : struct, IEntry;
    Meta GetSourceLocation(int id);

    void AppendFrame(Frame frame);
    (Timestamp Start, Timestamp End)? GetFrameRange();
    IEnumerable<Frame> QueryFrames(string module, Timestamp t0, Timestamp t1);
    (Timestamp Start, Timestamp End)? GetFrameAt(string module, Timestamp t);
}
