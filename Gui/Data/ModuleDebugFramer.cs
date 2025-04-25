using Tyr.Common.Time;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui.Data;

public class ModuleDebugFramer
{
    private readonly List<FrameData> _frames = [];
    private readonly Queue<Debug.Drawing.Command> _unassignedDraws = [];

    private Timestamp? _latestAssignedDrawTimestamp;
    private int? _latestSealedFrameIndex;
    private int FirstUnsealedFrameIndex => _latestSealedFrameIndex.GetValueOrDefault(-1) + 1;

    public int FrameCount => _frames.Count;
    public Timestamp? StartTime => _frames.FirstOrDefault()?.StartTimestamp;
    public Timestamp? EndTime => LatestFrame?.EndTimestamp;

    public FrameData? LatestFrame => _latestSealedFrameIndex.HasValue ? _frames[_latestSealedFrameIndex.Value] : null;

    public FrameData? GetFrame(Timestamp time)
    {
        if (StartTime is null || EndTime is null) return null;

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
                return frame;
            }
        }

        return null;
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
            if (IsDrawUnassignable(draw))
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
            _latestAssignedDrawTimestamp = draw.Meta.Timestamp;
        }

        SealFrames();
    }

    public void OnDraw(Debug.Drawing.Command draw)
    {
        // drop the draw if it never could be assigned
        if (IsDrawUnassignable(draw)) return;

        // check if we can already assign it to a frame
        var frame = GetFillFrame(draw.Meta.Timestamp);
        if (frame is not null)
        {
            frame.Draws.Add(draw); // already assignable to its frame
            _latestAssignedDrawTimestamp = draw.Meta.Timestamp;
            SealFrames();
        }
        else
        {
            _unassignedDraws.Enqueue(draw); // queue it for later
        }
    }

    private bool IsDrawUnassignable(Debug.Drawing.Command draw)
    {
        // handle the edge case where we've missed the frame this draw corresponds to
        // this should only happen for the first frame
        return _frames.Count > 0 &&
               _frames[0].StartTimestamp > draw.Meta.Timestamp;
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
        if (_latestAssignedDrawTimestamp is null) return;

        for (var index = FirstUnsealedFrameIndex; index < _frames.Count; index++)
        {
            var sealable =
                _frames[index].IsDefined &&
                _frames[index].EndTimestamp <= _latestAssignedDrawTimestamp;

            if (!sealable) break;

            _frames[index].IsSealed = true;
            _latestSealedFrameIndex = index;
        }
    }
}