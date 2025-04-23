using Debug = Tyr.Common.Debug;

namespace Tyr.Gui;

public class FrameData
{
    public Debug.Frame Frame { get; set; }
    public List<Debug.Drawing.Command> Draws { get; set; } = [];
}