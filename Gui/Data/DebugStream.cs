using Tyr.Common.Time;

namespace Tyr.Gui.Data;

public class DebugStream<T>
{
    private readonly Queue<T> _unassigned = [];

    private readonly Func<T, Timestamp> _getTimestamp;
    private readonly Func<T, bool> _isEmpty;
    private readonly Action<FrameData, T> _addToFrame;

    public Timestamp? LatestAssignedTimestamp { get; private set; }

    public DebugStream(
        Func<T, Timestamp> getTimestamp,
        Func<T, bool> isEmpty,
        Action<FrameData, T> addToFrame)
    {
        _getTimestamp = getTimestamp;
        _isEmpty = isEmpty;
        _addToFrame = addToFrame;
    }

    public void OnItem(
        T item,
        Func<Timestamp, bool> isUnassignable,
        Func<Timestamp, FrameData?> getFillFrame,
        Action sealFrames)
    {
        var timestamp = _getTimestamp(item);

        if (isUnassignable(timestamp)) return;

        var frame = getFillFrame(timestamp);
        if (frame is not null)
        {
            if (!_isEmpty(item))
                _addToFrame(frame, item);

            LatestAssignedTimestamp = timestamp;
            sealFrames();
        }
        else
        {
            _unassigned.Enqueue(item);
        }
    }

    public void DrainQueue(
        Func<Timestamp, bool> isUnassignable,
        Func<Timestamp, FrameData?> getFillFrame)
    {
        while (_unassigned.Count > 0)
        {
            var item = _unassigned.Peek();
            var timestamp = _getTimestamp(item);

            if (isUnassignable(timestamp))
            {
                _unassigned.Dequeue();
                continue;
            }

            var fillFrame = getFillFrame(timestamp);

            // items are in order; if the current one can't be assigned,
            // the rest can't either
            if (fillFrame is null) break;

            _unassigned.Dequeue();

            if (!_isEmpty(item))
                _addToFrame(fillFrame, item);

            LatestAssignedTimestamp = timestamp;
        }
    }
}
