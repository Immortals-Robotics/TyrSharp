using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using Tyr.Common.Config;
using Tyr.Common.Time;
using Tyr.Gui.Data;

namespace Tyr.Gui.Views;

[Configurable]
public class PlotView(DebugFramer debugFramer, DebugFilter filter)
{
    [ConfigEntry] private static int TimeAxisExtension { get; set; } = 5;

    private readonly (List<float> xs, List<float> ys) _plot = ([], []);

    public void Draw(PlaybackTime time)
    {
        if (ImGui.Begin($"{IconFonts.FontAwesome6.ChartLine} Plots"))
        {
            foreach (var (module, framer) in debugFramer.Modules)
            {
                if (!filter.IsEnabled(module)) continue;

                foreach (var (plotId, plotMeta) in framer.Plots)
                {
                    if (!filter.IsEnabled(plotMeta)) continue;

                    if (ImPlot.BeginPlot(plotId))
                    {
                        //ImPlot.SetupAxisScale(ImAxis.X1, ImPlotScale.Time);

                        var plot = ImPlot.GetCurrentPlot();
                        var xAxis = ImPlot.XAxis(plot, 0);

                        var end = time.Delta;
                        var start = end - DeltaTime.FromSeconds(xAxis.Range.Size());

                        // limits defined by the range [0, time] extended by 5s on each side
                        ImPlot.SetupAxisLimitsConstraints(ImAxis.X1, -TimeAxisExtension,
                            TimeAxisExtension + end.Seconds);

                        // snap to the latest data
                        xAxis.SetMax(Math.Max(TimeAxisExtension, end.Seconds));
                        if (!xAxis.Held && !plot.Held)
                        {
                            xAxis.SetMin(start.Seconds);
                        }

                        FillPlot(framer, plotId, time.StartTime, start, end);

                        var count = _plot.xs.Count;
                        if (count == 0) continue;

                        var xs = CollectionsMarshal.AsSpan(_plot.xs);
                        var ys = CollectionsMarshal.AsSpan(_plot.ys);

                        ImPlot.PlotLine("", ref xs[0], ref ys[0], count);

                        ImPlot.EndPlot();
                    }
                }
            }
        }

        ImGui.End();
    }

    private void FillPlot(ModuleDebugFramer framer, string id, Timestamp origin, DeltaTime min, DeltaTime max)
    {
        _plot.xs.Clear();
        _plot.ys.Clear();
        foreach (var frame in framer.GetFrameRange(origin + min, origin + max))
        {
            if (!frame.Plots.TryGetValue(id, out var plot)) continue;

            _plot.xs.Add((float)(frame.StartTimestamp - origin).Seconds);

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
            }
        }
    }
}