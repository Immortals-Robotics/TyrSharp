using Hexa.NET.ImGui;
using Tyr.Common.Debug.Db;
using Tyr.Common.Time;
using Tyr.Gui.Backend;

namespace Tyr.Gui.Views;

public class PlaybackControl(IDebugDb debugDb)
{
    private float _offset;
    private bool _live = true;
    private float _frozenRange;
    private Timestamp _frozenEndTime;

    public PlaybackTime Current
    {
        get
        {
            var liveEndTime = Tyr.Gui.Data.DebugDbUsageProfiler.Measure("PlaybackControl.GetFrameRange", () => debugDb.GetFrameRange())?.End ?? Timestamp.Zero;
            return new(_live, _live ? liveEndTime : _frozenEndTime, DeltaTime.FromSeconds(_offset));
        }
    }

    public void Draw()
    {
        if (ImGui.Begin($"{IconFonts.FontAwesome6.Clapperboard} Playback"))
        {
            ImGui.Button($"{IconFonts.FontAwesome6.BackwardStep}");
            ImGui.SameLine();
            ImGui.Button($"{IconFonts.FontAwesome6.Pause}");
            ImGui.SameLine();
            ImGui.Button($"{IconFonts.FontAwesome6.ForwardStep}");
            ImGui.SameLine();

            var wasLive = _live;
            var frameRange = Tyr.Gui.Data.DebugDbUsageProfiler.Measure("PlaybackControl.GetFrameRange", () => debugDb.GetFrameRange());
            var liveEndTime = frameRange?.End ?? Timestamp.Zero;
            var liveRange = frameRange.HasValue
                ? (float)(frameRange.Value.End - frameRange.Value.Start).Seconds
                : 0f;

            if (_live)
            {
                _frozenEndTime = liveEndTime;
                _frozenRange = liveRange;
                _offset = 0f;
            }
            else
            {
                _offset = Math.Clamp(_offset, -_frozenRange, 0f);
            }

            ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
            if (_live) ImGui.BeginDisabled();
            ImGui.SliderFloat("Time", ref _offset, -_frozenRange, 0f, ImGuiSliderFlags.None);
            if (_live) ImGui.EndDisabled();
            ImGui.PopFont();

            ImGui.SameLine();
            ImGui.Checkbox("Live", ref _live);

            if (!wasLive && _live)
            {
                _offset = 0f;
            }
        }

        ImGui.End();
    }
}
