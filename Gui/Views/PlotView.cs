using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using Tyr.Common.Time;
using Tyr.Gui.Data;

namespace Tyr.Gui.Views;

public class PlotView(DebugFramer debugFramer, DebugFilter filter)
{
    private readonly (List<float> xs, List<float> ys) _plot = ([], []);

    public void Draw(PlaybackTime time)
    {
        if (ImGui.Begin($"{IconFonts.FontAwesome6.ChartLine} Plots"))
        {
            foreach (var (module, framer) in debugFramer.Modules)
            {
                if (!filter.IsEnabled(module)) continue;

                var frame = time.Live ? framer.LatestFrame : framer.GetFrame(time.Time);
                if (frame == null) continue;

                // TODO: draw frame.Plots using implot
                for (var index = 0; index < frame.Plots.Count; index++)
                {
                    var plot = frame.Plots[index];
                    if (!filter.IsEnabled(plot.Meta)) continue;

                    Log.ZLogDebug($"Plot {plot.Meta.Expression} = {plot.Value}");

                    // Basic example of creating a plot
                    if (ImPlot.BeginPlot(plot.Meta.Expression))
                    {
                        var start = framer.StartTime.Value + DeltaTime.FromSeconds(ImPlot.ImPlotRange().Min);
                        var end = framer.StartTime.Value + DeltaTime.FromSeconds(ImPlot.ImPlotRange().Max);
                        
                        FillPlot(framer, index, framer.StartTime.Value, framer.EndTime.Value);
                        if (_plot.xs.Count == 0) continue;
                        
                        ImPlot.PlotLine("Data Series",
                            ref _plot.xs.ToArray()[0], ref _plot.ys.ToArray()[0],
                            _plot.xs.Count);

                        ImPlot.EndPlot();
                    }
                }
            }
        }

        ImGui.End();
    }

    private void FillPlot(ModuleDebugFramer framer, int idx, Timestamp start, Timestamp end)
    {
        _plot.xs.Clear();
        _plot.ys.Clear();
        foreach (var frame in framer.GetFrameRange(start, end))
        {
            if (idx >= frame.Plots.Count) continue;

            // TODO: this assumes constant number of plots in all frames
            var plot = frame.Plots[idx];
            _plot.xs.Add((float)(frame.StartTimestamp - framer.StartTime.Value).Seconds);

            switch (plot.Value)
            {
                case float f:
                    _plot.ys.Add(f);
                    break;

                case double d:
                    _plot.ys.Add((float)d);
                    break;

                case Vector2 v:
                    _plot.ys.Add(v.Length());
                    break;

                case Vector3 v:
                    _plot.ys.Add(v.Length());
                    break;

                default:
                    throw new NotImplementedException();
                    break;
            }
        }
    }
}