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

    private readonly List<float>[] _rawData = [[], [], [], [], []];

    private List<float> TimeList => _rawData[0];
    private List<float> ValueList => _rawData[4];
    private List<float> XList => _rawData[1];
    private List<float> YList => _rawData[2];
    private List<float> ZList => _rawData[3];
    private List<float> LenList => _rawData[4];

    private Span<float> TimeSpan => CollectionsMarshal.AsSpan(TimeList);
    private Span<float> ValueSpan => CollectionsMarshal.AsSpan(ValueList);
    private Span<float> XSpan => CollectionsMarshal.AsSpan(XList);
    private Span<float> YSpan => CollectionsMarshal.AsSpan(YList);
    private Span<float> ZSpan => CollectionsMarshal.AsSpan(ZList);
    private Span<float> LenSpan => CollectionsMarshal.AsSpan(LenList);

    enum PlotDataType
    {
        Float,
        Vector2,
        Vector3
    }

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

                    if (ImGui.CollapsingHeader(plotId))
                    {
                        if (ImPlot.BeginPlot("##plot"))
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

                            var type = FillPlot(framer, plotId, time.StartTime, start, end);

                            var count = TimeList.Count;
                            if (count == 0) continue;

                            switch (type)
                            {
                                case PlotDataType.Float:
                                    ImPlot.PlotLine("##value", ref TimeSpan[0], ref ValueSpan[0], count);
                                    break;
                                case PlotDataType.Vector2:
                                    ImPlot.PlotLine("x", ref TimeSpan[0], ref XSpan[0], count);
                                    ImPlot.PlotLine("y", ref TimeSpan[0], ref YSpan[0], count);
                                    ImPlot.PlotLine("len", ref TimeSpan[0], ref LenSpan[0], count);
                                    break;
                                case PlotDataType.Vector3:
                                    ImPlot.PlotLine("x", ref TimeSpan[0], ref XSpan[0], count);
                                    ImPlot.PlotLine("y", ref TimeSpan[0], ref YSpan[0], count);
                                    ImPlot.PlotLine("z", ref TimeSpan[0], ref ZSpan[0], count);
                                    ImPlot.PlotLine("len", ref TimeSpan[0], ref LenSpan[0], count);
                                    break;
                            }

                            ImPlot.EndPlot();
                        }
                    }
                }
            }
        }

        ImGui.End();
    }

    private PlotDataType FillPlot(ModuleDebugFramer framer, string id, Timestamp origin, DeltaTime min, DeltaTime max)
    {
        foreach (var list in _rawData) list.Clear();

        var type = PlotDataType.Float;

        foreach (var frame in framer.GetFrameRange(origin + min, origin + max))
        {
            if (!frame.Plots.TryGetValue(id, out var plot)) continue;

            TimeList.Add((float)(frame.StartTimestamp - origin).Seconds);

            switch (plot.Value)
            {
                case float f:
                    type = PlotDataType.Float;
                    ValueList.Add(f);
                    break;

                case double d:
                    type = PlotDataType.Float;
                    ValueList.Add((float)d);
                    break;

                case Vector2 v:
                    type = PlotDataType.Vector2;
                    XList.Add(v.X);
                    YList.Add(v.Y);
                    LenList.Add(v.Length());
                    break;

                case Vector3 v:
                    type = PlotDataType.Vector3;
                    XList.Add(v.X);
                    YList.Add(v.Y);
                    ZList.Add(v.Z);
                    LenList.Add(v.Length());
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        return type;
    }
}