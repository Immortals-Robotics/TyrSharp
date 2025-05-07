using Tyr.Common.Time;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui.Data;

public class ModuleDebugFramer
{
    private readonly List<FrameData> _frames = [];
    private readonly Queue<Debug.Logging.Entry> _unassignedLogs = [];
    private readonly Queue<Debug.Drawing.Command> _unassignedDraws = [];
    private readonly Queue<Debug.Plotting.Command> _unassignedPlots = [];

    private Timestamp? _latestAssignedLogTimestamp;
    private Timestamp? _latestAssignedDrawTimestamp;
    private Timestamp? _latestAssignedPlotTimestamp;

    private Timestamp? LatestAssignedCommandTimestamp =>
        !_latestAssignedLogTimestamp.HasValue ||
        !_latestAssignedDrawTimestamp.HasValue ||
        !_latestAssignedPlotTimestamp.HasValue
            ? null
            : Timestamp.Min(_latestAssignedLogTimestamp.Value,
                Timestamp.Min(_latestAssignedDrawTimestamp.Value, _latestAssignedPlotTimestamp.Value));

    private int? _latestSealedFrameIndex;
    private int FirstUnsealedFrameIndex => _latestSealedFrameIndex.GetValueOrDefault(-1) + 1;

    // layer -> file -> function -> MetaItem
    public Dictionary<string, Dictionary<string, Dictionary<string, HashSet<MetaTreeItem>>>> MetaTree { get; } = [];

    public Dictionary<string, Debug.Meta> Plots { get; } = [];

    public int FrameCount => _frames.Count;
    public Timestamp? StartTime => _frames.FirstOrDefault()?.StartTimestamp;
    public Timestamp? EndTime => LatestFrame?.EndTimestamp;

    public FrameData? LatestFrame => _latestSealedFrameIndex.HasValue ? _frames[_latestSealedFrameIndex.Value] : null;

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
            {
                right = mid - 1;
            }
            else if (time > frame.EndTimestamp)
            {
                left = mid + 1;
            }
            else
            {
                return mid;
            }
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
        {
            lastFrame.EndTimestamp = frame.StartTimestamp - DeltaTime.FromNanoseconds(1);
        }

        _frames.Add(new FrameData
        {
            StartTimestamp = frame.StartTimestamp
        });

        while (_unassignedLogs.Count > 0)
        {
            var log = _unassignedLogs.Peek();

            // drop the draw if it never could be assigned
            if (IsUnassignable(log.Timestamp))
            {
                _unassignedLogs.Dequeue();
                continue;
            }

            var fillFrame = GetFillFrame(log.Timestamp);

            // draws are in order, if the current one is not assignable,
            // then the next ones are also guaranteed not to be assignable
            if (fillFrame is null) break;

            // assignable, let's remove it from the queue
            _unassignedLogs.Dequeue();

            if (!log.IsEmpty)
            {
                fillFrame.Logs.Add(log);
                AddToMetaTree(log.Meta, MetaTreeItem.ItemType.Log);
            }

            _latestAssignedLogTimestamp = log.Timestamp;
        }

        while (_unassignedDraws.Count > 0)
        {
            var draw = _unassignedDraws.Peek();

            // drop the draw if it never could be assigned
            if (IsUnassignable(draw.Timestamp))
            {
                _unassignedDraws.Dequeue();
                continue;
            }

            var fillFrame = GetFillFrame(draw.Timestamp);

            // draws are in order, if the current one is not assignable,
            // then the next ones are also guaranteed not to be assignable
            if (fillFrame is null) break;

            // assignable, let's remove it from the queue
            _unassignedDraws.Dequeue();

            if (!draw.IsEmpty)
            {
                fillFrame.Draws.Add(draw);
                AddToMetaTree(draw.Meta, MetaTreeItem.ItemType.Draw);
            }

            _latestAssignedDrawTimestamp = draw.Timestamp;
        }

        while (_unassignedPlots.Count > 0)
        {
            var plot = _unassignedPlots.Peek();

            // drop the draw if it never could be assigned
            if (IsUnassignable(plot.Timestamp))
            {
                _unassignedPlots.Dequeue();
                continue;
            }

            var fillFrame = GetFillFrame(plot.Timestamp);

            // plots are in order, if the current one is not assignable,
            // then the next ones are also guaranteed not to be assignable
            if (fillFrame is null) break;

            // assignable, let's remove it from the queue
            _unassignedPlots.Dequeue();

            if (!plot.IsEmpty)
            {
                if (!fillFrame.Plots.TryAdd(plot.Id, plot))
                {
                    Log.ZLogWarning($"Dropping duplicate plot with id {plot.Id} to frame {fillFrame.StartTimestamp}");
                }
                else
                {
                    Plots[plot.Id] = plot.Meta;
                    AddToMetaTree(plot.Meta, MetaTreeItem.ItemType.Plot);
                }
            }

            _latestAssignedPlotTimestamp = plot.Timestamp;
        }

        SealFrames();
    }

    public void OnLog(Debug.Logging.Entry log)
    {
        // drop the draw if it never could be assigned
        if (IsUnassignable(log.Timestamp)) return;

        // check if we can already assign it to a frame
        var frame = GetFillFrame(log.Timestamp);
        if (frame is not null)
        {
            if (!log.IsEmpty)
            {
                frame.Logs.Add(log); // already assignable to its frame
                AddToMetaTree(log.Meta, MetaTreeItem.ItemType.Log);
            }

            _latestAssignedLogTimestamp = log.Timestamp;
            SealFrames();
        }
        else
        {
            _unassignedLogs.Enqueue(log); // queue it for later
        }
    }

    public void OnDraw(Debug.Drawing.Command draw)
    {
        // drop the draw if it never could be assigned
        if (IsUnassignable(draw.Timestamp)) return;

        // check if we can already assign it to a frame
        var frame = GetFillFrame(draw.Timestamp);
        if (frame is not null)
        {
            if (!draw.IsEmpty)
            {
                frame.Draws.Add(draw); // already assignable to its frame
                AddToMetaTree(draw.Meta, MetaTreeItem.ItemType.Draw);
            }

            _latestAssignedDrawTimestamp = draw.Timestamp;
            SealFrames();
        }
        else
        {
            _unassignedDraws.Enqueue(draw); // queue it for later
        }
    }

    public void OnPlot(Debug.Plotting.Command plot)
    {
        // drop the draw if it never could be assigned
        if (IsUnassignable(plot.Timestamp)) return;

        // check if we can already assign it to a frame
        var frame = GetFillFrame(plot.Timestamp);
        if (frame is not null)
        {
            // already assignable to its frame
            if (!plot.IsEmpty)
            {
                if (!frame.Plots.TryAdd(plot.Id, plot))
                {
                    Log.ZLogWarning($"Dropping duplicate plot with id {plot.Id} to frame {frame.StartTimestamp}");
                }
                else
                {
                    Plots[plot.Id] = plot.Meta;
                    AddToMetaTree(plot.Meta, MetaTreeItem.ItemType.Plot);
                }
            }

            _latestAssignedPlotTimestamp = plot.Timestamp;
            SealFrames();
        }
        else
        {
            _unassignedPlots.Enqueue(plot); // queue it for later
        }
    }

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

    private bool IsUnassignable(Timestamp timestamp)
    {
        // handle the edge case where we've missed the frame this draw corresponds to
        // this should only happen for the first frame
        return _frames.Count > 0 &&
               _frames[0].StartTimestamp > timestamp;
    }

    private FrameData? GetFillFrame(Timestamp time)
    {
        if (_frames.Count == 0) return null;

        for (var index = FirstUnsealedFrameIndex; index < _frames.Count; index++)
        {
            Assert.IsFalse(_frames[index].IsSealed);

            // we don't know the time range of this frame yet, so we can't assign it
            if (!_frames[index].IsDefined) break;

            if (_frames[index].StartTimestamp <= time && time <= _frames[index].EndTimestamp)
                return _frames[index];
        }

        return null;
    }

    // seal the frames up to the latest assigned draw timestamp
    private void SealFrames()
    {
        if (LatestAssignedCommandTimestamp is null) return;

        for (var index = FirstUnsealedFrameIndex; index < _frames.Count; index++)
        {
            var sealable =
                _frames[index].IsDefined &&
                _frames[index].EndTimestamp <= LatestAssignedCommandTimestamp;

            if (!sealable) break;

            _frames[index].Logs.TrimExcess();
            _frames[index].Draws.TrimExcess();
            _frames[index].Plots.TrimExcess();

            _frames[index].IsSealed = true;
            _latestSealedFrameIndex = index;
        }
    }
}