using Tyr.Common.Time;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui.Data;

public class ModuleTimeline
{
    private readonly List<FrameData> _frames = [];
    private readonly DebugStream<Debug.Logging.Entry> _logs;
    private readonly DebugStream<Debug.Drawing.Command> _draws;
    private readonly DebugStream<Debug.Plotting.Command> _plots;

    private Timestamp? LatestAssignedCommandTimestamp
    {
        get
        {
            if (!_logs.LatestAssignedTimestamp.HasValue ||
                !_draws.LatestAssignedTimestamp.HasValue ||
                !_plots.LatestAssignedTimestamp.HasValue)
                return null;

            return Timestamp.Min(_logs.LatestAssignedTimestamp.Value,
                Timestamp.Min(_draws.LatestAssignedTimestamp.Value, _plots.LatestAssignedTimestamp.Value));
        }
    }

    private int? _latestSealedFrameIndex;
    private int FirstUnsealedFrameIndex => _latestSealedFrameIndex.GetValueOrDefault(-1) + 1;

    private readonly Func<Timestamp, bool> _isUnassignable;
    private readonly Func<Timestamp, FrameData?> _getFillFrame;
    private readonly Action _sealFrames;

    // layer -> file -> function -> MetaItem
    public Dictionary<string, Dictionary<string, Dictionary<string, HashSet<MetaTreeItem>>>> MetaTree { get; } = [];

    public Dictionary<string, Debug.Meta> Plots { get; } = [];

    public int FrameCount => _frames.Count;
    public Timestamp? StartTime => _frames.FirstOrDefault()?.StartTimestamp;
    public Timestamp? EndTime => LatestFrame?.EndTimestamp;

    public FrameData? LatestFrame => _latestSealedFrameIndex.HasValue ? _frames[_latestSealedFrameIndex.Value] : null;

    public ModuleTimeline()
    {
        _isUnassignable = IsUnassignable;
        _getFillFrame = GetFillFrame;
        _sealFrames = SealFrames;

        _logs = new DebugStream<Debug.Logging.Entry>(
            e => e.Timestamp,
            e => e.IsEmpty,
            (frame, e) =>
            {
                frame.Logs.Add(e);
                AddToMetaTree(e.Meta, MetaTreeItem.ItemType.Log);
            });

        _draws = new DebugStream<Debug.Drawing.Command>(
            d => d.Timestamp,
            d => d.IsEmpty,
            (frame, d) =>
            {
                frame.Draws.Add(d);
                AddToMetaTree(d.Meta, MetaTreeItem.ItemType.Draw);
            });

        _plots = new DebugStream<Debug.Plotting.Command>(
            p => p.Timestamp,
            p => p.IsEmpty,
            (frame, p) =>
            {
                if (!frame.Plots.TryAdd(p.Id, p))
                {
                    Log.ZLogWarning($"Dropping duplicate plot with id {p.Id} to frame {frame.StartTimestamp}");
                }
                else
                {
                    Plots[p.Id] = p.Meta;
                    AddToMetaTree(p.Meta, MetaTreeItem.ItemType.Plot);
                }
            });
    }

    private int GetFrameIndex(Timestamp time)
    {
        if (StartTime is null || EndTime is null) return -1;

        time = Timestamp.Clamp(time, StartTime.Value, EndTime.Value);

        var left = 0;
        var right = _latestSealedFrameIndex!.Value;

        while (left <= right)
        {
            var mid = left + (right - left) / 2;
            var frame = _frames[mid];

            if (time < frame.StartTimestamp)
                right = mid - 1;
            else if (time > frame.EndTimestamp)
                left = mid + 1;
            else
                return mid;
        }

        return -1;
    }

    public FrameData? GetFrame(Timestamp time)
    {
        var index = GetFrameIndex(time);
        return index >= 0 ? _frames[index] : null;
    }

    public IEnumerable<FrameData> GetFrameRange(Timestamp startTime, Timestamp endTime, int? maxCount = null)
    {
        if (StartTime is null || EndTime is null) yield break;

        startTime = Timestamp.Clamp(startTime, StartTime.Value, EndTime.Value);
        endTime = Timestamp.Clamp(endTime, StartTime.Value, EndTime.Value);

        var startIdx = GetFrameIndex(startTime);
        var endIdx = GetFrameIndex(endTime);
        if (startIdx < 0 || endIdx < 0) yield break;

        var count = endIdx - startIdx + 1;
        var step = maxCount.HasValue ? int.Max(1, count / maxCount.Value) : 1;

        for (var i = startIdx; i <= _latestSealedFrameIndex!.Value; i += step)
        {
            var frame = _frames[i];
            if (frame.EndTimestamp < startTime) continue;
            if (frame.StartTimestamp > endTime) yield break;
            yield return frame;
        }
    }

    public void OnFrame(Debug.Frame frame)
    {
        if (_frames.LastOrDefault() is { } lastFrame)
            lastFrame.EndTimestamp = frame.StartTimestamp - DeltaTime.FromNanoseconds(1);

        _frames.Add(new FrameData { StartTimestamp = frame.StartTimestamp });

        _logs.DrainQueue(_isUnassignable, _getFillFrame);
        _draws.DrainQueue(_isUnassignable, _getFillFrame);
        _plots.DrainQueue(_isUnassignable, _getFillFrame);

        SealFrames();
    }

    public void OnLog(Debug.Logging.Entry log) =>
        _logs.OnItem(log, _isUnassignable, _getFillFrame, _sealFrames);

    public void OnDraw(Debug.Drawing.Command draw) =>
        _draws.OnItem(draw, _isUnassignable, _getFillFrame, _sealFrames);

    public void OnPlot(Debug.Plotting.Command plot) =>
        _plots.OnItem(plot, _isUnassignable, _getFillFrame, _sealFrames);

    private void AddToMetaTree(Debug.Meta meta, MetaTreeItem.ItemType type)
    {
        if (meta is { File: not null, Member: not null })
        {
            if (!MetaTree.TryGetValue(meta.Layer, out var fileDict))
            {
                fileDict = [];
                MetaTree[meta.Layer] = fileDict;
            }

            if (!fileDict.TryGetValue(meta.File, out var functionDict))
            {
                functionDict = [];
                fileDict[meta.File] = functionDict;
            }

            if (!functionDict.TryGetValue(meta.Member, out var lineSet))
            {
                lineSet = [];
                functionDict[meta.Member] = lineSet;
            }

            var item = MetaTreeItem.GetOrCreate(type, meta.Line, meta.Expression);
            lineSet.Add(item);
        }
        else
        {
            Log.ZLogWarning($"Failed to add item of type {type} with null meta to the tree: {meta}");
        }
    }

    private bool IsUnassignable(Timestamp timestamp) =>
        _frames.Count > 0 && _frames[0].StartTimestamp > timestamp;

    private FrameData? GetFillFrame(Timestamp time)
    {
        if (_frames.Count == 0) return null;

        var left = FirstUnsealedFrameIndex;
        var right = _frames.Count - 1;

        // The last frame may not have EndTimestamp set yet (not IsDefined),
        // so shrink the search range to only defined frames.
        while (right >= left && !_frames[right].IsDefined)
            right--;

        if (right < left) return null;

        while (left <= right)
        {
            var mid = left + (right - left) / 2;
            var frame = _frames[mid];

            if (time < frame.StartTimestamp)
                right = mid - 1;
            else if (time > frame.EndTimestamp)
                left = mid + 1;
            else
                return frame;
        }

        return null;
    }

    private void SealFrames()
    {
        if (LatestAssignedCommandTimestamp is null) return;

        for (var index = FirstUnsealedFrameIndex; index < _frames.Count; index++)
        {
            var sealable =
                _frames[index].IsDefined &&
                _frames[index].EndTimestamp <= LatestAssignedCommandTimestamp;

            if (!sealable) break;

            /*_frames[index].Logs.TrimExcess();
            _frames[index].Draws.TrimExcess();
            _frames[index].Plots.TrimExcess();*/

            _frames[index].IsSealed = true;
            _latestSealedFrameIndex = index;
        }
    }
}
