using Debug = Tyr.Common.Debug;

namespace Tyr.Gui;

public class ModuleDebugFramer
{
    private readonly LinkedList<FrameData> _frames = [];

    public int FrameCount => _frames.Count;

    public FrameData? LatestDisplayableFrame => _frames.Count > 1 ? _frames.Last!.Previous!.Value : null;

    public FrameData? GetClosestFrame(Timestamp time)
    {
        return _frames
            .OrderBy(f => Math.Abs((f.Frame.StartTimestamp - time).Seconds))
            .FirstOrDefault();
    }

    private FrameData? GetDrawFrame(Timestamp time)
    {
        var frame = _frames.Last;
        while (frame is not null)
        {
            if (frame.Value.Frame.StartTimestamp <= time)
                return frame.Value;

            frame = frame.Previous;
        }

        return null;
    }

    public void OnFrame(Debug.Frame frame)
    {
        _frames.AddLast(new FrameData
        {
            Frame = frame,
            Draws = []
        });
    }

    public void OnDraw(Debug.Drawing.Command draw)
    {
        // TODO: handle data arrived before the frame
        var frame = GetDrawFrame(draw.Meta.Timestamp);
        frame?.Draws.Add(draw);
    }
}