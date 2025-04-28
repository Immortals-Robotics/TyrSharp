using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using Tyr.Common.Config;
using Tyr.Common.Time;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;

namespace Tyr.Gui.Views;

[Configurable]
public class PlotView(DebugFramer debugFramer, DebugFilter filter)
{
    [ConfigEntry] private static double TimeAxisExtension { get; set; } = 5;
    [ConfigEntry] private static double TimeAxisMinRange { get; set; } = 1;
    [ConfigEntry] private static double TimeAxisMaxRange { get; set; } = 200;

    [ConfigEntry] private static double YAxisMinRange { get; set; } = 1;
    [ConfigEntry] private static int MaxPoints { get; set; } = 500;

    private readonly List<float>[] _rawData =
    [
        new(MaxPoints),
        new(MaxPoints),
        new(MaxPoints),
        new(MaxPoints),
        new(MaxPoints),
    ];

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

    private DeltaTime _linkedTimeRange = DeltaTime.Zero;

    private ImGuiTextFilterPtr _filter = ImGui.ImGuiTextFilter();
    private bool IsFiltering => _filter.IsActive();
    private int _filterTested;
    private int _filterPassed;

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
            _filterTested = 0;
            _filterPassed = 0;

            DrawSearchBar();
            DrawPlots(time);

            // Add status bar at bottom
            if (IsFiltering)
            {
                ImGui.Separator();
                ImGui.TextDisabled($"{_filterPassed} of {_filterTested} items matching");
            }
        }

        ImGui.End();
    }

    private void DrawSearchBar()
    {
        ImGui.PushItemWidth(-24); // Make space for the clear button
        _filter.Draw("##search");
        ImGui.PopItemWidth();

        ImGui.SameLine();
        if (IsFiltering)
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.Xmark}##clear"))
            {
                _filter.Clear();
            }
        }
        else
        {
            ImGui.TextDisabled($"{IconFonts.FontAwesome6.MagnifyingGlass}");
        }

        ImGui.Separator();
    }

    private void DrawPlots(PlaybackTime time)
    {
        ImGui.PushFont(FontRegistry.Instance.MonoFont);

        foreach (var framer in debugFramer.Modules.Values)
        {
            foreach (var (plotId, plotMeta) in framer.Plots)
            {
                if (!filter.IsEnabled(plotMeta)) continue;

                _filterTested += 1;
                if (!_filter.PassFilter(plotId)) continue;
                _filterPassed += 1;

                ImGui.PushID(plotId);

                var open = ImGui.CollapsingHeader(plotId);
                ImGui.SameLine();
                ImGui.TextUnformatted("   ");
                ImGui.SameLine();
                ImGui.TextDisabled(plotMeta.Expression);

                if (open)
                {
                    if (DrawPlot(time, framer, plotId)) continue;
                }

                ImGui.PopID();
            }
        }

        ImGui.PopFont();
    }

    private bool DrawPlot(PlaybackTime time, ModuleDebugFramer framer, string plotId)
    {
        if (ImPlot.BeginPlot("##plot"))
        {
            // limits defined by the range [0, ∞) extended by the extension factor 
            ImPlot.SetupAxisLimitsConstraints(ImAxis.X1, -TimeAxisExtension, float.PositiveInfinity);
            ImPlot.SetupAxisZoomConstraints(ImAxis.X1, TimeAxisMinRange, TimeAxisMaxRange);
            ImPlot.SetupAxisZoomConstraints(ImAxis.Y1, YAxisMinRange, double.PositiveInfinity);

            ImPlot.SetupAxis(ImAxis.X1, "Time (s)");

            var plot = ImPlot.GetCurrentPlot();
            var xAxis = ImPlot.XAxis(plot, 0);

            // the active plot can override the range, others will follow
            if (plot.Hovered || xAxis.Hovered)
                _linkedTimeRange = DeltaTime.FromSeconds(xAxis.Range.Size());

            var end = time.Delta;

            var extensionCausedRange = Math.Clamp(3 * TimeAxisExtension - end.Seconds,
                0, 2 * TimeAxisExtension);
            var minRange = Math.Max(TimeAxisMinRange, extensionCausedRange);
            _linkedTimeRange = DeltaTime.Max(_linkedTimeRange, DeltaTime.FromSeconds(minRange));

            var start = xAxis.Held || plot.Held
                ? DeltaTime.FromSeconds(xAxis.Range.Min)
                : end - _linkedTimeRange;

            // snap to the latest data
            xAxis.SetMax(Math.Max(TimeAxisExtension, end.Seconds));
            xAxis.SetMin(start.Seconds);

            var (type, title) = GatherData(framer, plotId, time.StartTime, start, end);

            if (title != null)
            {
                ImPlot.SetupAxis(ImAxis.Y1, title, ImPlotAxisFlags.AutoFit);
            }
            else
            {
                ImPlot.SetupAxis(ImAxis.Y1, ImPlotAxisFlags.AutoFit | ImPlotAxisFlags.NoLabel);
            }

            var count = TimeList.Count;
            if (count == 0) return true;

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
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ImPlot.EndPlot();
        }

        return false;
    }

    private (PlotDataType type, string? title) GatherData(
        ModuleDebugFramer framer, string id,
        Timestamp origin, DeltaTime min, DeltaTime max)
    {
        foreach (var list in _rawData) list.Clear();

        var type = PlotDataType.Float;
        string? title = null;

        var frames = framer.GetFrameRange(origin + min, origin + max, MaxPoints);
        foreach (var frame in frames)
        {
            if (!frame.Plots.TryGetValue(id, out var plot)) continue;

            title ??= plot.Title;

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

                case int i:
                    type = PlotDataType.Float;
                    ValueList.Add(i);
                    break;

                case bool b:
                    type = PlotDataType.Float;
                    ValueList.Add(b ? 1f : 0f);
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

        return (type, title);
    }
}