using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using Tyr.Common.Config;
using Tyr.Common.Debug.Db;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Plotting;
using Tyr.Common.Time;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Command = Tyr.Common.Debug.Plotting.Command;

namespace Tyr.Gui.Views;

[Configurable]
public partial class PlotView(DebugFramer debugFramer, DebugFilter filter, IDebugDb debugDb)
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
    private readonly Dictionary<string, Dictionary<string, Common.Debug.Meta>> _plotMetadataCache = [];

    private ImGuiTextFilterPtr _filter = ImGui.ImGuiTextFilter();
    private bool IsFiltering => _filter.IsActive();
    private int _filterTested;
    private int _filterPassed;

    enum PlotDataType
    {
        Scalar,
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
                ImGui.TextColored(Color.Zinc400, $"{_filterPassed} of {_filterTested} items matching");
            }
        }

        ImGui.End();
    }

    private void DrawSearchBar()
    {
        ImGui.SetNextItemWidth(-24); // Make space for the clear button
        _filter.Draw("##search");

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

        foreach (var (moduleName, _) in debugFramer.Modules)
        {
            var plots = GetPlots(moduleName).ToArray();
            var anyVisible = plots.Any(kv =>
                filter.IsEnabled(kv.Value) && _filter.PassFilter(kv.Key));

            if (!anyVisible) continue;

            ImGui.PushID(moduleName);
            var sectionOpen = ImGui.CollapsingHeader(moduleName, ImGuiTreeNodeFlags.DefaultOpen);
            if (sectionOpen)
            {
                ImGui.Indent();
                foreach (var (plotId, plotMeta) in plots)
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
                        DrawPlot(time, moduleName, plotId);
                    }

                    ImGui.PopID();
                }
                ImGui.Unindent();
            }
            ImGui.PopID();
        }

        ImGui.PopFont();
    }

    private IEnumerable<KeyValuePair<string, Common.Debug.Meta>> GetPlots(string moduleName)
    {
        if (!_plotMetadataCache.TryGetValue(moduleName, out var plotMetadata))
        {
            plotMetadata = [];
            _plotMetadataCache[moduleName] = plotMetadata;
        }

        foreach (var plotId in debugDb.QueryShardKeys<Command>(moduleName))
        {
            if (!plotMetadata.TryGetValue(plotId, out var plotMeta))
            {
                plotMeta = TryGetPlotMeta(moduleName, plotId);
                if (plotMeta == Common.Debug.Meta.Empty)
                    continue;

                plotMetadata[plotId] = plotMeta;
            }

            yield return new KeyValuePair<string, Common.Debug.Meta>(plotId, plotMeta);
        }
    }

    private Common.Debug.Meta TryGetPlotMeta(string moduleName, string plotId)
    {
        foreach (var plot in debugDb.Query<Command>(moduleName, Timestamp.Zero, Timestamp.MaxValue, plotId, 1))
            return plot.Meta;

        return Common.Debug.Meta.Empty;
    }

    private void DrawPlot(PlaybackTime time, string moduleName, string plotId)
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

            var (type, title) = GatherData(moduleName, plotId, time.StartTime, start, end);

            if (title != null)
            {
                ImPlot.SetupAxis(ImAxis.Y1, title, ImPlotAxisFlags.AutoFit);
            }
            else
            {
                ImPlot.SetupAxis(ImAxis.Y1, ImPlotAxisFlags.AutoFit | ImPlotAxisFlags.NoLabel);
            }

            var count = TimeList.Count;
            if (count == 0)
            {
                ImPlot.EndPlot();
                return;
            }

            switch (type)
            {
                case PlotDataType.Scalar:
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
    }

    private (PlotDataType type, string? title) GatherData(
        string moduleName, string id,
        Timestamp origin, DeltaTime min, DeltaTime max)
    {
        foreach (var list in _rawData) list.Clear();

        var type = PlotDataType.Scalar;
        string? title = null;

        var startTimestamp = origin + min;
        var endTimestamp = origin + max;

        foreach (var plot in debugDb.Query<Command>(moduleName, startTimestamp, endTimestamp, id, MaxPoints))
        {
            title ??= plot.Title;
            TimeList.Add((float)(plot.Timestamp - origin).Seconds);
            AddPlotValue(plot.Value, ref type);
        }

        return (type, title);
    }

    private void AddPlotValue(PlotValue value, ref PlotDataType type)
    {
        switch (value.Kind)
        {
            case PlotValueKind.Number:
                type = PlotDataType.Scalar;
                ValueList.Add((float)value.Number);
                break;

            case PlotValueKind.Boolean:
                type = PlotDataType.Scalar;
                ValueList.Add(value.Boolean ? 1f : 0f);
                break;

            case PlotValueKind.Vector2:
                var v2 = value.Vector2Value;
                type = PlotDataType.Vector2;
                XList.Add(v2.X);
                YList.Add(v2.Y);
                LenList.Add(v2.Length());
                break;

            case PlotValueKind.Vector3:
                var v3 = value.Vector3Value;
                type = PlotDataType.Vector3;
                XList.Add(v3.X);
                YList.Add(v3.Y);
                ZList.Add(v3.Z);
                LenList.Add(v3.Length());
                break;

            default:
                throw new NotImplementedException();
        }
    }
}
