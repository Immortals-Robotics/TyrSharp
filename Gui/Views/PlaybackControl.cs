using Hexa.NET.ImGui;
using Tyr.Common.Time;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;

namespace Tyr.Gui.Views;

public class PlaybackControl(DebugFramer debugFramer)
{
    private float _offset;
    private bool _live = true;
    private float _frozenRange;
    private Timestamp _frozenEndTime;

    public PlaybackTime Current => new(_live, _live ? debugFramer.EndTime : _frozenEndTime, DeltaTime.FromSeconds(_offset));

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

            if (_live)
            {
                _frozenEndTime = debugFramer.EndTime;
                _frozenRange = (float)debugFramer.Duration.Seconds;
                _offset = 0f;
            }
            else
            {
                _offset = Math.Clamp(_offset, -_frozenRange, 0f);
            }

            ImGui.PushFont(FontRegistry.Instance.MonoFont);
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
