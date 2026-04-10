using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Debug.Db;
using Tyr.Common.Time;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;

using Stopwatch = System.Diagnostics.Stopwatch;

namespace Tyr.Gui.Views;

[Configurable]
public sealed partial class PlaybackControl(IDebugDb debugDb, PlaybackSessionManager playbackSessions)
{
    [ConfigEntry(StorageType.User)] private static double StepMilliseconds { get; set; } = 16.0;
    [ConfigEntry(StorageType.User)] private static double ShuttleMaxSpeedSecondsPerSecond { get; set; } = 0.5;

    private float _offset;
    private bool _live = true;
    private bool _playing;
    private float _shuttle;
    private float _frozenRange;
    private Timestamp _frozenEndTime;
    private long _lastDrawTimestamp = Stopwatch.GetTimestamp();

    public PlaybackTime Current { get; private set; } = CreatePlaybackTime(
        debugDb.GetFrameRange()?.End ?? Timestamp.Zero,
        live: true,
        frozenEndTime: Timestamp.Zero,
        offset: 0f);

    public void Draw()
    {
        var nowTicks = Stopwatch.GetTimestamp();
        var elapsedSeconds = (nowTicks - _lastDrawTimestamp) / (double)Stopwatch.Frequency;
        _lastDrawTimestamp = nowTicks;

        if (ImGui.Begin($"{IconFonts.FontAwesome6.Clapperboard} Playback", ImGuiWindowFlags.NoResize))
        {
            var frameRange = debugDb.GetFrameRange();
            var liveEndTime = frameRange?.End ?? Timestamp.Zero;
            var liveRange = frameRange.HasValue
                ? (float)(frameRange.Value.End - frameRange.Value.Start).Seconds
                : 0f;

            var wasLive = _live;
            if (_live)
            {
                _playing = false;
                _frozenEndTime = liveEndTime;
                _frozenRange = liveRange;
                _offset = 0f;
            }
            else
            {
                _offset = Math.Clamp(_offset, -_frozenRange, 0f);
                AdvancePlayback(elapsedSeconds);
                AdvanceShuttle(elapsedSeconds);
            }

            if (_live)
                ImGui.BeginDisabled();

            if (ImGui.Button($"{IconFonts.FontAwesome6.BackwardStep}"))
                StepBy(-1);

            if (_live)
                ImGui.EndDisabled();

            ImGui.SameLine();
            if (_live)
                ImGui.BeginDisabled();

            if (ImGui.Button(_playing
                    ? $"{IconFonts.FontAwesome6.Pause} Pause"
                    : $"{IconFonts.FontAwesome6.Play} Play"))
            {
                _playing = !_playing;
            }

            if (_live)
                ImGui.EndDisabled();

            ImGui.SameLine();
            if (_live)
                ImGui.BeginDisabled();

            if (ImGui.Button($"{IconFonts.FontAwesome6.ForwardStep}"))
                StepBy(1);

            if (_live)
                ImGui.EndDisabled();

            ImGui.SameLine();
            ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
            ImGui.SetNextItemWidth(Math.Max(180f, ImGui.GetContentRegionAvail().X - 280f));
            if (_live)
                ImGui.BeginDisabled();

            if (ImGui.SliderFloat("##time", ref _offset, -_frozenRange, 0f, "%.3fs"))
                _playing = false;

            if (_live)
                ImGui.EndDisabled();
            ImGui.PopFont();

            ImGui.SameLine();
            ImGui.Checkbox("Live", ref _live);

            ImGui.SameLine();
            if (ImGui.Button($"{IconFonts.FontAwesome6.FolderOpen} {playbackSessions.CurrentSourceLabel}"))
                ImGui.OpenPopup("PlaybackSource");

            DrawSourcePopup();

            if (!_live)
            {
                ImGui.SameLine();
                ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
                ImGui.SetNextItemWidth(120f);
                if (ImGui.SliderFloat("##shuttle", ref _shuttle, -1f, 1f, "%.2f"))
                    _playing = false;
                if (ImGui.IsItemDeactivatedAfterEdit())
                    _shuttle = 0f;
                ImGui.PopFont();
            }

            if (!wasLive && _live)
            {
                _offset = 0f;
                _shuttle = 0f;
                _playing = false;
            }
            else if (wasLive && !_live)
            {
                _frozenEndTime = liveEndTime;
                _frozenRange = liveRange;
                _offset = 0f;
            }

            Current = CreatePlaybackTime(liveEndTime, _live, _frozenEndTime, _offset);
        }

        ImGui.End();
    }

    private static PlaybackTime CreatePlaybackTime(Timestamp liveEndTime, bool live, Timestamp frozenEndTime, float offset)
    {
        return new PlaybackTime(live, live ? liveEndTime : frozenEndTime, DeltaTime.FromSeconds(offset));
    }

    private void StepBy(int direction)
    {
        if (direction == 0)
            return;

        FreezeIfLive();
        _playing = false;
        _shuttle = 0f;

        var stepSeconds = (float)(StepMilliseconds / 1000.0 * direction);
        _offset = Math.Clamp(_offset + stepSeconds, -_frozenRange, 0f);
    }

    private void AdvancePlayback(double elapsedSeconds)
    {
        if (!_playing || elapsedSeconds <= 0d)
            return;

        _offset = Math.Clamp(_offset + (float)elapsedSeconds, -_frozenRange, 0f);
        if (_offset >= 0f)
        {
            _offset = 0f;
            _playing = false;
        }
    }

    private void AdvanceShuttle(double elapsedSeconds)
    {
        if (elapsedSeconds <= 0d || Math.Abs(_shuttle) < 0.001f)
            return;

        var direction = MathF.Pow(Math.Abs(_shuttle), 3f) * MathF.Sign(_shuttle);
        var delta = direction * (float)ShuttleMaxSpeedSecondsPerSecond * (float)elapsedSeconds;
        _offset = Math.Clamp(_offset + delta, -_frozenRange, 0f);

        if (_offset >= 0f)
        {
            _offset = 0f;
            _shuttle = 0f;
        }
    }

    private void FreezeIfLive()
    {
        if (!_live)
            return;

        var frameRange = debugDb.GetFrameRange();
        _frozenEndTime = frameRange?.End ?? Timestamp.Zero;
        _frozenRange = frameRange.HasValue
            ? (float)(frameRange.Value.End - frameRange.Value.Start).Seconds
            : 0f;
        _offset = 0f;
        _live = false;
    }

    private void DrawSourcePopup()
    {
        if (!ImGui.BeginPopup("PlaybackSource"))
            return;

        if (ImGui.Selectable($"{IconFonts.FontAwesome6.SatelliteDish} Live", playbackSessions.UsingLive))
        {
            playbackSessions.SwitchToLive();
            _live = true;
            _offset = 0f;
            _playing = false;
            _shuttle = 0f;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton($"{IconFonts.FontAwesome6.Rotate} Refresh"))
            playbackSessions.RefreshSessions();

        ImGui.Separator();

        foreach (var session in playbackSessions.Sessions)
        {
            var selected = !playbackSessions.UsingLive &&
                           string.Equals(playbackSessions.CurrentSessionMetadataPath, session.MetadataPath, StringComparison.Ordinal);
            if (!ImGui.Selectable(session.DisplayName, selected))
                continue;

            playbackSessions.OpenSession(session);
            _live = true;
            _offset = 0f;
            _playing = false;
            _shuttle = 0f;
        }

        ImGui.EndPopup();
    }
}
