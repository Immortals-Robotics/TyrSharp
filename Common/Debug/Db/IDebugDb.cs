using Tyr.Common.Debug.Logging;

namespace Tyr.Common.Debug.Db;

public interface IDebugDb : IDisposable
{
    void Append<T>(T entry) where T : struct, IEntry;

    /// <summary>
    /// Appends every entry of <typeparamref name="T"/> in <paramref name="module"/> with a timestamp in
    /// [t0, t1] to <paramref name="destination"/>, oldest first, without allocating per entry.
    /// <paramref name="metaFilter"/> is evaluated once per shard (source location), not per entry, so
    /// disabled call sites are skipped before any deserialization. With <paramref name="maxCount"/> the
    /// result is thinned to roughly that many samples spread over the window.
    /// Returns the number of entries added.
    /// </summary>
    int QueryInto<T>(List<T> destination, string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null, Func<Meta, bool>? metaFilter = null) where T : struct, IEntry;

    /// <summary>As the module overload, with every filter optional (null = any).</summary>
    int QueryInto<T>(List<T> destination, Timestamp t0, Timestamp t1, string? module = null, int? sourceLocationId = null, string? shardKey = null, int? maxCount = null, Func<Meta, bool>? metaFilter = null) where T : struct, IEntry;

    IEnumerable<T> Query<T>(string module, Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry;
    IEnumerable<T> Query<T>(Timestamp t0, Timestamp t1, string? module = null, int? sourceLocationId = null, string? shardKey = null, int? maxCount = null) where T : struct, IEntry;
    IEnumerable<T> QueryAll<T>(Timestamp t0, Timestamp t1, string? shardKey = null, int? maxCount = null) where T : struct, IEntry;
    IEnumerable<string> QueryModules();
    IEnumerable<string> QueryShardKeys<T>(string module) where T : struct, IEntry;
    IEnumerable<Meta> QuerySourceLocations<T>(string module) where T : struct, IEntry;
    IEnumerable<Meta> QuerySourceLocations(string module, Type type);
    Meta? TryGetShardMeta<T>(string module, string shardKey) where T : struct, IEntry;
    Meta GetSourceLocation(int id);

    void FillJournal(List<JournalGroup> destination);
    void RebuildJournal(bool group);

    void AppendFrame(Frame frame);
    (Timestamp Start, Timestamp End)? GetFrameRange();
    IEnumerable<Frame> QueryFrames(string module, Timestamp t0, Timestamp t1);
    (Timestamp Start, Timestamp End)? GetFrameAt(string module, Timestamp t);
}
