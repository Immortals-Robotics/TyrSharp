using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using System.Runtime.InteropServices;
using Tyr.Common.Config;
using Tyr.Common.Debug.Db;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Plotting;
using Tyr.Common.Time;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Command = Tyr.Common.Debug.Plotting.Command;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui.Views;

[Configurable]
public partial class PlotView(IDebugDb debugDb)
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.ChartLine} Plots";
    [ConfigEntry] private static partial double TimeAxisMinRange { get; set; } = 1;
    [ConfigEntry] private static partial double TimeAxisMaxRange { get; set; } = 200;

    [ConfigEntry] private static partial double YAxisMinRange { get; set; } = 1;
    [ConfigEntry] private static partial int MaxPoints { get; set; } = 500;

    private DeltaTime _linkedHistoryRange = DeltaTime.Zero;
    private readonly HashSet<PlotKey> _openPlots = [];
    private bool _prepareStateDirty = true;
    private PrepareState? _prepareStateCache;
    private DeltaTime _prepareStateRange;

    private ImGuiTextFilterPtr _filter = ImGui.ImGuiTextFilter();
    private bool IsFiltering => _filter.IsActive();
    private int _filterTested;
    private int _filterPassed;

    internal enum PlotDataType
    {
        Scalar,
        Vector2,
        Vector3
    }

    internal readonly record struct PlotKey(string ModuleName, string PlotId);

    internal sealed record PrepareState(DeltaTime LinkedHistoryRange, IReadOnlySet<PlotKey> OpenPlots);

    internal sealed class PreparedSeries
    {
        public PlotDataType Type { get; set; }
        public DeltaTime Min { get; set; }
        public DeltaTime Max { get; set; }
        public List<float> Time { get; } = new(MaxPoints);
        public List<float> Value { get; } = new(MaxPoints);
        public List<float> X { get; } = new(MaxPoints);
        public List<float> Y { get; } = new(MaxPoints);
        public List<float> Z { get; } = new(MaxPoints);
        public List<float> Len { get; } = new(MaxPoints);

        public void Reset()
        {
            Type = PlotDataType.Scalar;
            Min = DeltaTime.Zero;
            Max = DeltaTime.Zero;
            Time.Clear();
            Value.Clear();
            X.Clear();
            Y.Clear();
            Z.Clear();
            Len.Clear();
        }
    }

    internal sealed class PreparedPlot
    {
        public string PlotId { get; set; } = string.Empty;
        public Common.Debug.Meta Meta { get; set; } = Common.Debug.Meta.Empty;
        public PreparedSeries? Series { get; private set; }

        public PreparedSeries EnsureSeries()
        {
            Series ??= new PreparedSeries();
            return Series;
        }

        public void Reset()
        {
            PlotId = string.Empty;
            Meta = Common.Debug.Meta.Empty;
            Series?.Reset();
        }
    }

    internal sealed class PreparedModule
    {
        private readonly Stack<PreparedPlot> _plotPool = [];

        public string ModuleName { get; set; } = string.Empty;
        public List<PreparedPlot> Plots { get; } = [];

        public PreparedPlot AddPlot(string plotId, Common.Debug.Meta meta)
        {
            var plot = _plotPool.Count > 0 ? _plotPool.Pop() : new PreparedPlot();
            plot.PlotId = plotId;
            plot.Meta = meta;
            Plots.Add(plot);
            return plot;
        }

        public void Reset()
        {
            foreach (var plot in Plots)
            {
                plot.Reset();
                _plotPool.Push(plot);
            }

            ModuleName = string.Empty;
            Plots.Clear();
        }
    }

    internal sealed class PreparedData
    {
        private readonly Stack<PreparedModule> _modulePool = [];

        public List<PreparedModule> Modules { get; } = [];

        public PreparedModule AddModule(string moduleName)
        {
            var module = _modulePool.Count > 0 ? _modulePool.Pop() : new PreparedModule();
            module.ModuleName = moduleName;
            Modules.Add(module);
            return module;
        }

        public void Reset()
        {
            foreach (var module in Modules)
            {
                module.Reset();
                _modulePool.Push(module);
            }

            Modules.Clear();
        }
    }

    internal PrepareState CreatePrepareState()
    {
        if (_prepareStateCache is null || _prepareStateDirty || _prepareStateRange != _linkedHistoryRange)
        {
            _prepareStateRange = _linkedHistoryRange;
            _prepareStateCache = new PrepareState(_linkedHistoryRange, new HashSet<PlotKey>(_openPlots));
            _prepareStateDirty = false;
        }

        return _prepareStateCache;
    }

    internal void Prepare(PlaybackTime time, DebugFilterSnapshot filterSnapshot, PrepareState state, PreparedData prepared)
    {
        prepared.Reset();
        var historyRange = ClampLinkedHistoryRange(state.LinkedHistoryRange);

        foreach (var moduleName in debugDb.QueryModules())
        {
            if (!filterSnapshot.IsEnabled(moduleName))
                continue;

            PreparedModule? preparedModule = null;
            foreach (var plotId in debugDb.QueryShardKeys<Command>(moduleName))
            {
                var plotMeta = debugDb.TryGetShardMeta<Command>(moduleName, plotId) ?? Common.Debug.Meta.Empty;
                if (plotMeta == Common.Debug.Meta.Empty || !filterSnapshot.IsEnabled(plotMeta))
                    continue;

                preparedModule ??= prepared.AddModule(moduleName);
                var preparedPlot = preparedModule.AddPlot(plotId, plotMeta);
                if (state.OpenPlots.Contains(new PlotKey(moduleName, plotId)))
                {
                    var (min, max) = GetAnchoredTimeRange(time.Offset, historyRange);
                    GatherPreparedData(moduleName, plotId, time.EndTime, min, max, preparedPlot.EnsureSeries());
                }
            }
        }
    }

    internal void Draw(PreparedData prepared, bool isLive)
    {
        if (ImGui.Begin(WindowTitle))
        {
            _filterTested = 0;
            _filterPassed = 0;

            DrawSearchBar();
            DrawPlots(prepared);

            // Add status bar at bottom
            if (IsFiltering)
            {
                ImGui.Separator();
                ImGui.TextColored(Color.Zinc400, $"{_filterPassed} of {_filterTested} items matching");
            }

            if (!isLive)
            {
                // Draw a red border to indicate that we're showing playback data, not live data.
                var min = ImGui.GetWindowPos();
                var max = min + ImGui.GetWindowSize();
                ImGui.GetWindowDrawList().AddRect(min, max, ImGui.ColorConvertFloat4ToU32(Color.Red), 0f, ImDrawFlags.None, 4f);
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

    private void DrawPlots(PreparedData prepared)
    {
        ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);

        foreach (var module in prepared.Modules)
        {
            var anyVisible = module.Plots.Any(plot => _filter.PassFilter(plot.PlotId));

            if (!anyVisible) continue;

            ImGui.PushID(module.ModuleName);
            var sectionOpen = ImGui.CollapsingHeader(module.ModuleName, ImGuiTreeNodeFlags.DefaultOpen);
            if (sectionOpen)
            {
                ImGui.Indent();
                foreach (var plot in module.Plots)
                {
                    _filterTested += 1;
                    if (!_filter.PassFilter(plot.PlotId)) continue;
                    _filterPassed += 1;

                    ImGui.PushID(plot.PlotId);

                    var open = ImGui.CollapsingHeader(plot.PlotId);
                    UpdateOpenState(module.ModuleName, plot.PlotId, open);
                    ImGui.SameLine();
                    ImGui.TextUnformatted("   ");
                    ImGui.SameLine();
                    ImGui.TextDisabled(plot.Meta.Expression);

                    if (open)
                    {
                        if (plot.Series is null)
                        {
                            ImGui.TextDisabled("Preparing plot data...");
                        }
                        else
                        {
                            DrawPlot(plot.Series);
                        }
                    }

                    ImGui.PopID();
                }
                ImGui.Unindent();
            }
            ImGui.PopID();
        }

        ImGui.PopFont();
    }

    private void UpdateOpenState(string moduleName, string plotId, bool open)
    {
        var plotKey = new PlotKey(moduleName, plotId);
        if (open)
            _prepareStateDirty |= _openPlots.Add(plotKey);
        else
            _prepareStateDirty |= _openPlots.Remove(plotKey);
    }

    private void DrawPlot(PreparedSeries series)
    {
        if (ImPlot.BeginPlot("##plot"))
        {
            ImPlot.SetupAxisZoomConstraints(ImAxis.X1, TimeAxisMinRange, TimeAxisMaxRange);
            ImPlot.SetupAxisZoomConstraints(ImAxis.Y1, YAxisMinRange, double.PositiveInfinity);

            ImPlot.SetupAxis(ImAxis.X1, "Time (s)");

            var plot = ImPlot.GetCurrentPlot();
            var xAxis = ImPlot.XAxis(plot, 0);

            if (plot.Hovered || xAxis.Hovered)
                _linkedHistoryRange = DeltaTime.FromSeconds(xAxis.Range.Size());

            _linkedHistoryRange = ClampLinkedHistoryRange(_linkedHistoryRange);

            if (!xAxis.Held && !plot.Held)
            {
                xAxis.SetMin(series.Max.Seconds - _linkedHistoryRange.Seconds);
                xAxis.SetMax(series.Max.Seconds);
            }

            ImPlot.SetupAxis(ImAxis.Y1, ImPlotAxisFlags.AutoFit | ImPlotAxisFlags.NoLabel);
            
            var count = series.Time.Count;
            if (count == 0)
            {
                ImPlot.EndPlot();
                return;
            }

            var time = CollectionsMarshal.AsSpan(series.Time);
            var value = CollectionsMarshal.AsSpan(series.Value);
            var x = CollectionsMarshal.AsSpan(series.X);
            var y = CollectionsMarshal.AsSpan(series.Y);
            var z = CollectionsMarshal.AsSpan(series.Z);
            var len = CollectionsMarshal.AsSpan(series.Len);

            switch (series.Type)
            {
                case PlotDataType.Scalar:
                    ImPlot.PlotLine("##value", ref time[0], ref value[0], count);
                    break;
                case PlotDataType.Vector2:
                    ImPlot.PlotLine("x", ref time[0], ref x[0], count);
                    ImPlot.PlotLine("y", ref time[0], ref y[0], count);
                    ImPlot.PlotLine("len", ref time[0], ref len[0], count);
                    break;
                case PlotDataType.Vector3:
                    ImPlot.PlotLine("x", ref time[0], ref x[0], count);
                    ImPlot.PlotLine("y", ref time[0], ref y[0], count);
                    ImPlot.PlotLine("z", ref time[0], ref z[0], count);
                    ImPlot.PlotLine("len", ref time[0], ref len[0], count);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ImPlot.EndPlot();
        }
    }

    private static DeltaTime ClampLinkedHistoryRange(DeltaTime historyRange)
    {
        return DeltaTime.Clamp(
            historyRange,
            DeltaTime.FromSeconds(TimeAxisMinRange),
            DeltaTime.FromSeconds(TimeAxisMaxRange));
    }

    private static (DeltaTime Min, DeltaTime Max) GetAnchoredTimeRange(DeltaTime currentTime, DeltaTime historyRange)
    {
        var max = currentTime;
        var min = currentTime - historyRange;
        return (min, max);
    }

    private void GatherPreparedData(
        string moduleName, string id,
        Timestamp origin, DeltaTime min, DeltaTime max, PreparedSeries prepared)
    {
        prepared.Reset();
        prepared.Type = PlotDataType.Scalar;
        prepared.Min = min;
        prepared.Max = max;

        var startTimestamp = origin + min;
        var endTimestamp = origin + max;

        foreach (var plot in debugDb.QueryWithoutMeta<Command>(moduleName, startTimestamp, endTimestamp, id, MaxPoints))
        {
            prepared.Time.Add((float)(plot.Timestamp - origin).Seconds);
            AddPlotValue(plot.Value, prepared);
        }
    }

    private static void AddPlotValue(PlotValue value, PreparedSeries prepared)
    {
        switch (value.Kind)
        {
            case PlotValueKind.Number:
                prepared.Type = PlotDataType.Scalar;
                prepared.Value.Add((float)value.Number);
                break;

            case PlotValueKind.Boolean:
                prepared.Type = PlotDataType.Scalar;
                prepared.Value.Add(value.Boolean ? 1f : 0f);
                break;

            case PlotValueKind.Vector2:
                var v2 = value.Vector2Value;
                prepared.Type = PlotDataType.Vector2;
                prepared.X.Add(v2.X);
                prepared.Y.Add(v2.Y);
                prepared.Len.Add(v2.Length());
                break;

            case PlotValueKind.Vector3:
                var v3 = value.Vector3Value;
                prepared.Type = PlotDataType.Vector3;
                prepared.X.Add(v3.X);
                prepared.Y.Add(v3.Y);
                prepared.Z.Add(v3.Z);
                prepared.Len.Add(v3.Length());
                break;

            default:
                throw new NotImplementedException();
        }
    }

}
