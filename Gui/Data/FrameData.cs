using Debug = Tyr.Common.Debug;

namespace Tyr.Gui.Data;

public class FrameData
{
    public Timestamp StartTimestamp { get; init; }
    public Timestamp? EndTimestamp { get; set; }

    public bool IsDefined => EndTimestamp.HasValue;
    public bool IsSealed { get; set; }

    public List<Debug.Drawing.Command> Draws { get; } = [];
    public Dictionary<string, Debug.Plotting.Command> Plots { get; } = [];
}