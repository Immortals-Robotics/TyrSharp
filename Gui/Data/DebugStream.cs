namespace Tyr.Gui.Data;

public class DebugStream<T> where T : Common.Debug.IEntry
{
    private readonly Queue<T> _unassigned = [];

    private readonly Action<FrameData, T> _addToFrame;

    public Timestamp? LatestAssignedTimestamp { get; private set; }

    public DebugStream(
        Action<FrameData, T> addToFrame)
    {
        _addToFrame = addToFrame;
    }

    public void OnItem(
        T item,
        Func<Timestamp, bool> isUnassignable,
        Func<Timestamp, FrameData?> getFillFrame,
        Action sealFrames)
    {
        var timestamp = item.Timestamp;

        if (isUnassignable(timestamp)) return;

        var frame = getFillFrame(timestamp);
        if (frame is not null)
        {
            if (!item.IsEmpty)
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
            var timestamp = item.Timestamp;

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

            if (!item.IsEmpty)
                _addToFrame(fillFrame, item);

            LatestAssignedTimestamp = timestamp;
        }
    }
}
