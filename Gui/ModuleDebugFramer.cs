using Tyr.Common.Time;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui;

public class ModuleDebugFramer
{
    private readonly LinkedList<FrameData> _frames = [];
    private readonly Queue<Debug.Drawing.Command> _unassignedDraws = [];

    private Timestamp? _latestAssignedDrawTimestamp;
    private LinkedListNode<FrameData>? _latestSealedFrame;

    public int FrameCount => _frames.Count;

    public FrameData? LatestDisplayableFrame => _latestSealedFrame?.Value;

    public void OnFrame(Debug.Frame frame)
    {
        if (_frames.Last is not null)
        {
            _frames.Last.ValueRef.EndTimestamp = frame.StartTimestamp - DeltaTime.FromNanoseconds(1);
        }
        _frames.AddLast(new FrameData
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
        return _frames.First is not null &&
               draw.Meta.Timestamp < _frames.First.Value.StartTimestamp;
    }

    private FrameData? GetFillFrame(Timestamp time)
    {
        if (_frames.Count == 0) return null;

        var current = _latestSealedFrame?.Next ?? _frames.First;
        while (current is not null)
        {
            Assert.IsFalse(current.Value.IsSealed);

            // we don't know the time range of this frame yet, so we can't assign it
            if (!current.Value.IsDefined) break;

            if (current.Value.StartTimestamp <= time && time <= current.Value.EndTimestamp)
                return current.Value;

            current = current.Next;
        }

        return null;
    }

    // seal the frames up to the latest assigned draw timestamp
    private void SealFrames()
    {
        if (_latestAssignedDrawTimestamp is null) return;

        for (var current = _latestSealedFrame ?? _frames.First; current is not null; current = current.Next)
        {
            var sealable =
                current.Value.IsDefined &&
                current.Value.EndTimestamp <= _latestAssignedDrawTimestamp;

            if (!sealable) break;

            current.ValueRef.IsSealed = true;
            _latestSealedFrame = current;
        }
    }
}