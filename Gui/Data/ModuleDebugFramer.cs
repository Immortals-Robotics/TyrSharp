using Tyr.Common.Time;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui.Data;

public class ModuleDebugFramer
{
    private readonly List<FrameData> _frames = [];
    private readonly Queue<Debug.Drawing.Command> _unassignedDraws = [];
    private readonly Queue<Debug.Plotting.Command> _unassignedPlots = [];

    private Timestamp? _latestAssignedDrawTimestamp;
    private Timestamp? _latestAssignedPlotTimestamp;

    private Timestamp? LatestAssignedCommandTimestamp =>
        !_latestAssignedDrawTimestamp.HasValue || !_latestAssignedPlotTimestamp.HasValue
            ? null
            : Timestamp.Min(_latestAssignedDrawTimestamp.Value, _latestAssignedPlotTimestamp.Value);

    private int? _latestSealedFrameIndex;
    private int FirstUnsealedFrameIndex => _latestSealedFrameIndex.GetValueOrDefault(-1) + 1;

    // file -> function -> MetaItem
    public Dictionary<string, Dictionary<string, SortedSet<MetaTreeItem>>> MetaTree { get; } = [];

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

        while (_unassignedDraws.Count > 0)
        {
            var draw = _unassignedDraws.Peek();

            // drop the draw if it never could be assigned
            if (IsUnassignable(draw.Meta))
            {
                _unassignedDraws.Dequeue();
                continue;
            }

            var drawFrame = GetFillFrame(draw.Meta.Timestamp);

            // draws are in order, if the current one is not assignable,
            // then the next ones are also guaranteed not to be assignable
            if (drawFrame is null) break;

            // assignable, let's remove it from the queue
            _unassignedDraws.Dequeue();

            drawFrame.Draws.Add(draw);
            AddToMetaTree(draw.Meta, MetaTreeItem.ItemType.Draw);
            _latestAssignedDrawTimestamp = draw.Meta.Timestamp;
        }

        while (_unassignedPlots.Count > 0)
        {
            var plot = _unassignedPlots.Peek();

            // drop the draw if it never could be assigned
            if (IsUnassignable(plot.Meta))
            {
                _unassignedPlots.Dequeue();
                continue;
            }

            var fillFrame = GetFillFrame(plot.Meta.Timestamp);

            // plots are in order, if the current one is not assignable,
            // then the next ones are also guaranteed not to be assignable
            if (fillFrame is null) break;

            // assignable, let's remove it from the queue
            _unassignedPlots.Dequeue();

            fillFrame.Plots.Add(plot.Id, plot);
            Plots[plot.Id] = plot.Meta;
            AddToMetaTree(plot.Meta, MetaTreeItem.ItemType.Plot);
            _latestAssignedPlotTimestamp = plot.Meta.Timestamp;
        }

        SealFrames();
    }

    public void OnDraw(Debug.Drawing.Command draw)
    {
        // drop the draw if it never could be assigned
        if (IsUnassignable(draw.Meta)) return;

        // check if we can already assign it to a frame
        var frame = GetFillFrame(draw.Meta.Timestamp);
        if (frame is not null)
        {
            frame.Draws.Add(draw); // already assignable to its frame
            AddToMetaTree(draw.Meta, MetaTreeItem.ItemType.Draw);
            _latestAssignedDrawTimestamp = draw.Meta.Timestamp;
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
        if (IsUnassignable(plot.Meta)) return;

        // check if we can already assign it to a frame
        var frame = GetFillFrame(plot.Meta.Timestamp);
        if (frame is not null)
        {
            frame.Plots.Add(plot.Id, plot); // already assignable to its frame
            Plots[plot.Id] = plot.Meta;
            AddToMetaTree(plot.Meta, MetaTreeItem.ItemType.Plot);
            _latestAssignedPlotTimestamp = plot.Meta.Timestamp;
            SealFrames();
        }
        else
        {
            _unassignedPlots.Enqueue(plot); // queue it for later
        }
    }

    private void AddToMetaTree(Debug.Meta meta, MetaTreeItem.ItemType type)
    {
        if (meta is { FilePath: not null, MemberName: not null, Expression: not null })
        {
            if (!MetaTree.TryGetValue(meta.FilePath, out var functionDict))
            {
                functionDict = [];
                MetaTree[meta.FilePath] = functionDict;
            }

            if (!functionDict.TryGetValue(meta.MemberName, out var lineSet))
            {
                lineSet = [];
                functionDict[meta.MemberName] = lineSet;
            }

            var item = new MetaTreeItem(type, meta.LineNumber, meta.Expression);
            lineSet.Add(item);
        }
        else
        {
            Log.ZLogWarning($"Failed to add item of type {type} with null meta to the tree: {meta}");
        }
    }

    private bool IsUnassignable(Debug.Meta meta)
    {
        // handle the edge case where we've missed the frame this draw corresponds to
        // this should only happen for the first frame
        return _frames.Count > 0 &&
               _frames[0].StartTimestamp > meta.Timestamp;
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

            _frames[index].IsSealed = true;
            _latestSealedFrameIndex = index;
        }
    }
}