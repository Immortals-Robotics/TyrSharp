using Hexa.NET.ImGui;
using Tyr.Common.Time;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;

namespace Tyr.Gui.Views;

public class PlaybackControl(DebugFramer debugFramer)
{
    private float _time;
    private bool _live = true;

    public PlaybackTime Current => new(_live, debugFramer.StartTime + DeltaTime.FromSeconds(_time));

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
            
            if (_live) _time = (float)debugFramer.Duration.Seconds;

            ImGui.PushFont(FontRegistry.Instance.MonoFont);
            if (_live) ImGui.BeginDisabled();
            ImGui.SliderFloat("Time", ref _time, 0f, (float)debugFramer.Duration.Seconds, ImGuiSliderFlags.None);
            if (_live) ImGui.EndDisabled();
            ImGui.PopFont();

            ImGui.SameLine();
            ImGui.Checkbox("Live", ref _live);
        }

        ImGui.End();
    }
}